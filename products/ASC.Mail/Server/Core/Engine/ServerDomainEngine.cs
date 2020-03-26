/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security;
using ASC.Common.Logging;
using ASC.Common.Threading;
using ASC.Common.Utils;
using ASC.Core;
using ASC.Mail.Core.Engine.Operations;
using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Core.Entities;
using ASC.Mail.Models;
using ASC.Mail.Extensions;
using ASC.Mail.Utils;
using ASC.Web.Core;
using SecurityContext = ASC.Core.SecurityContext;
using Microsoft.Extensions.Options;

namespace ASC.Mail.Core.Engine
{
    public class ServerDomainEngine
    {
        public int Tenant
        {
            get
            {
                return TenantManager.GetCurrentTenant().TenantId;
            }
        }

        public string User
        {
            get
            {
                return SecurityContext.CurrentAccount.ID.ToString();
            }
        }

        private bool IsAdmin
        {
            get
            {
                return WebItemSecurity.IsProductAdministrator(WebItemManager.MailProductID, SecurityContext.CurrentAccount.ID);
            }
        }
        public SecurityContext SecurityContext { get; }
        public TenantManager TenantManager { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public WebItemSecurity WebItemSecurity { get; }
        public EngineFactory EngineFactory { get; }
        public DaoFactory DaoFactory { get; }

        public ILog Log { get; private set; }

        public ServerDomainEngine(
            SecurityContext securityContext,
            TenantManager tenantManager,
            CoreBaseSettings coreBaseSettings,
            WebItemSecurity webItemSecurity,
            EngineFactory engineFactory,
            DaoFactory daoFactory,
            IOptionsMonitor<ILog> option)
        {
            SecurityContext = securityContext;
            TenantManager = tenantManager;
            CoreBaseSettings = coreBaseSettings;
            WebItemSecurity = webItemSecurity;
            EngineFactory = engineFactory;
            DaoFactory = daoFactory;

            Log = option.Get("ASC.Mail.ServerDomainEngine");
        }

        public ServerDomainData GetDomain(int id)
        {
            var serverDomain = DaoFactory.ServerDomainDao.GetDomain(id);

            if (serverDomain == null)
                throw new Exception("Domain not found");

            var dnsData = UpdateDnsStatus(serverDomain);

            return ToServerDomainData(serverDomain, dnsData);
        }

        public List<ServerDomainData> GetDomains()
        {
            if (!IsAdmin)
                throw new SecurityException("Need admin privileges.");

            var listDomains = DaoFactory.ServerDomainDao.GetDomains();

            if (!listDomains.Any())
                return new List<ServerDomainData>();

            var list = new List<ServerDomainData>();

            foreach (var domain in listDomains)
            {
                var dnsData = UpdateDnsStatus(domain);

                var serverDomain = ToServerDomainData(domain, dnsData);

                list.Add(serverDomain);
            }

            return list;
        }

        public ServerDomainData GetCommonDomain()
        {
            var domainCommon = DaoFactory.ServerDomainDao.GetDomains()
                .SingleOrDefault(x => x.Tenant == Defines.SHARED_TENANT_ID);

            if (domainCommon == null)
                return null;

            var serverDomain = ToServerDomainData(domainCommon, null);

            return serverDomain;
        }

        public ServerDomainDnsData GetDnsData(int domainId)
        {
            if (!IsAdmin)
                throw new SecurityException("Need admin privileges.");

            if (domainId < 0)
                throw new ArgumentException(@"Invalid domain id.", "domainId");

            var domain = DaoFactory.ServerDomainDao.GetDomain(domainId);

            if (domain == null)
                return null;

            var dnsData = UpdateDnsStatus(domain, true);

            return dnsData;
        }

        public bool IsDomainExists(string name)
        {
            if (!IsAdmin)
                throw new SecurityException("Need admin privileges.");

            if (string.IsNullOrEmpty(name))
                throw new ArgumentException(@"Invalid domain name.", "name");

            if (name.Length > 255)
                throw new ArgumentException(@"Domain name exceed limitation of 255 characters.", "name");

            if (!Parser.IsDomainValid(name))
                throw new ArgumentException(@"Incorrect domain name.", "name");

            var domainName = name.ToLowerInvariant();

            return DaoFactory.ServerDomainDao.IsDomainExists(domainName);
        }

        public ServerDomainData AddDomain(string domain, int dnsId)
        {
            if (!IsAdmin)
                throw new SecurityException("Need admin privileges.");

            if (string.IsNullOrEmpty(domain))
                throw new ArgumentException(@"Invalid domain name.", "domain");

            if (domain.Length > 255)
                throw new ArgumentException(@"Domain name exceed limitation of 255 characters.", "domain");

            if (!Parser.IsDomainValid(domain))
                throw new ArgumentException(@"Incorrect domain name.", "domain");

            var domainName = domain.ToLowerInvariant();

            var dnsLookup = new DnsLookup();

                var server = DaoFactory.ServerDao.Get(Tenant);

                var freeDns = EngineFactory.ServerEngine.GetOrCreateUnusedDnsData(DaoFactory, server);

                if (freeDns.Id != dnsId)
                    throw new InvalidDataException("This dkim public key is already in use. Please reopen wizard again.");

                if (!CoreBaseSettings.Standalone &&
                    !dnsLookup.IsDomainTxtRecordExists(domainName, freeDns.DomainCheckRecord.Value))
                {
                    throw new InvalidOperationException("txt record is not correct.");
                }

                var isVerified = freeDns.CheckDnsStatus(domainName);

                using (var tx = DaoFactory.BeginTransaction())
                {
                    var utcNow = DateTime.UtcNow;

                    var mailServerEngine = new Server.Core.ServerEngine(server.Id, server.ConnectionString);

                    var mailServerDomain = new Server.Core.Entities.Domain
                    {
                        Name = domainName,
                        Active = true,
                        Description = string.Format("Domain created in UtcTime: {0}, for tenant: {1}", utcNow, Tenant),
                        Created = utcNow,
                        Modified = utcNow
                    };

                    mailServerEngine.SaveDomain(mailServerDomain);

                    var serverDomain = new ServerDomain
                    {
                        Id = 0,
                        Tenant = Tenant,
                        Name = domainName,
                        IsVerified = isVerified,
                        DateAdded = utcNow,
                        DateChecked = utcNow
                    };

                    serverDomain.Id = DaoFactory.ServerDomainDao
                        .Save(serverDomain);

                    var serverDns = DaoFactory.ServerDnsDao
                        .GetById(freeDns.Id);

                    var mailServerDkim = new Server.Core.Entities.Dkim
                    {
                        DomainName = domainName,
                        Selector = serverDns.DkimSelector,
                        PrivateKey = serverDns.DkimPrivateKey,
                        PublicKey = serverDns.DkimPublicKey
                    };

                    mailServerEngine.SaveDkim(mailServerDkim);

                    serverDns.DomainId = serverDomain.Id;
                    serverDns.TimeModified = utcNow;

                    DaoFactory.ServerDnsDao.Save(serverDns);

                    tx.Commit();

                    return ToServerDomainData(serverDomain, freeDns);
                }
        }

        public MailOperationStatus RemoveDomain(int id)
        {
            if (!IsAdmin)
                throw new SecurityException("Need admin privileges.");

            if (id < 0)
                throw new ArgumentException(@"Invalid domain id.", "id");

            var domain = GetDomain(id);

            if (domain.IsSharedDomain)
                throw new SecurityException("Can not remove shared domain.");

            var tenant = TenantManager.GetCurrentTenant();
            var user = SecurityContext.CurrentAccount;

            var operationEngine = new OperationEngine();

            var operations = operationEngine.MailOperations.GetTasks()
                .Where(o =>
                {
                    var oTenant = o.GetProperty<int>(MailOperation.TENANT);
                    var oUser = o.GetProperty<string>(MailOperation.OWNER);
                    var oType = o.GetProperty<MailOperationType>(MailOperation.OPERATION_TYPE);
                    return oTenant == tenant.TenantId &&
                           oUser == user.ID.ToString() &&
                           oType == MailOperationType.RemoveDomain;
                })
                .ToList();

            var sameOperation = operations.FirstOrDefault(o =>
            {
                var oSource = o.GetProperty<string>(MailOperation.SOURCE);
                return oSource == domain.Id.ToString();
            });

            if (sameOperation != null)
            {
                return operationEngine.GetMailOperationStatus(sameOperation.Id);
            }

            var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

            if (runningOperation != null)
                throw new MailOperationAlreadyRunningException("Remove mailbox operation already running.");

            var op = new MailRemoveMailserverDomainOperation(tenant, user, domain);

            return operationEngine.QueueTask(op);
        }

        private ServerDomainDnsData UpdateDnsStatus(ServerDomain domain, bool force = false)
        {
            var serverDns = DaoFactory.ServerDnsDao.Get(domain.Id);

            if (serverDns.UpdateRecords(EngineFactory, domain.Name, force))
            {
                DaoFactory.ServerDnsDao.Save(serverDns);
            }

            var dnsData = ToServerDomainDnsData(serverDns);

            if (dnsData != null && domain.IsVerified != dnsData.IsVerified)
            {
                DaoFactory.ServerDomainDao.SetVerified(domain.Id, dnsData.IsVerified);
            }

            return dnsData;
        }

        protected ServerDomainData ToServerDomainData(ServerDomain domain, ServerDomainDnsData dns)
        {
            var serverDomain = new ServerDomainData
            {
                Id = domain.Id,
                Name = domain.Name,
                Dns = dns,
                IsSharedDomain = domain.Tenant == Defines.SHARED_TENANT_ID
            };

            return serverDomain;
        }

        protected ServerDomainDnsData ToServerDomainDnsData(ServerDns serverDns)
        {
            if (serverDns == null)
                return null;

            var dnsData = new ServerDomainDnsData
            {
                Id = serverDns.Id,
                MxRecord = new ServerDomainMxRecordData
                {
                    Host = serverDns.Mx,
                    IsVerified = serverDns.MxVerified,
                    Priority = Defines.ServerDnsMxRecordPriority
                },
                SpfRecord = new ServerDomainDnsRecordData
                {
                    Name = Defines.DNS_DEFAULT_ORIGIN,
                    IsVerified = serverDns.SpfVerified,
                    Value = serverDns.Spf
                },
                DkimRecord = new ServerDomainDkimRecordData
                {
                    Selector = serverDns.DkimSelector,
                    IsVerified = serverDns.DkimVerified,
                    PublicKey = serverDns.DkimPublicKey
                },
                DomainCheckRecord = new ServerDomainDnsRecordData
                {
                    Name = Defines.DNS_DEFAULT_ORIGIN,
                    IsVerified = true,
                    Value = serverDns.DomainCheck
                }
            };

            return dnsData;
        }
    }
}