/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
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


using ASC.Api.Core;
using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Dao;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Entities;
using ASC.Mail.Enums;
using ASC.Mail.Models;
using ASC.Mail.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace ASC.Mail.Core.Engine
{
    public class SignatureEngine
    {
        public DbContextManager<MailDbContext> DbContext { get; }

        public int Tenant
        {
            get
            {
                return ApiContext.Tenant.TenantId;
            }
        }

        public string UserId
        {
            get
            {
                return SecurityContext.CurrentAccount.ID.ToString();
            }
        }

        public SecurityContext SecurityContext { get; }

        public ApiContext ApiContext { get; }

        public ILog Log { get; }

        public DaoFactory DaoFactory { get; }
        public CacheEngine CacheEngine { get; }
        public MailDbContext MailDb { get; }

        public SignatureEngine(
            DbContextManager<MailDbContext> dbContext,
            ApiContext apiContext,
            SecurityContext securityContext,
            IOptionsMonitor<ILog> option,
            DaoFactory daoFactory,
            CacheEngine cacheEngine)
        {
            ApiContext = apiContext;
            SecurityContext = securityContext;
            Log = option.Get("ASC.Mail.SignatureEngine");

            MailDb = dbContext.Get("mail");
            DaoFactory = daoFactory;
            CacheEngine = cacheEngine;
        }

        public MailSignatureData GetSignature(int mailboxId)
        {
            return ToMailMailSignature(DaoFactory.MailboxSignatureDao.GetSignature(mailboxId));
        }

        public MailSignatureData SaveSignature(int mailboxId, string html, bool isActive)
        {
            if (!string.IsNullOrEmpty(html))
            {
                //TODO: Fix after StorageManager implementation
                /*var imagesReplacer = new StorageManager(Tenant, User, Log);
                html = imagesReplacer.ChangeEditorImagesLinks(html, mailboxId);*/
            }

            CacheEngine.Clear(UserId);

            var signature = new MailboxSignature
            {
                MailboxId = mailboxId,
                Tenant = Tenant,
                Html = html,
                IsActive = isActive
            };

            var result = DaoFactory.MailboxSignatureDao.SaveSignature(signature);

            if (result <= 0)
                throw new Exception("Save failed");

            return ToMailMailSignature(signature);
        }

        protected MailSignatureData ToMailMailSignature(MailboxSignature signature)
        {
            return new MailSignatureData(signature.MailboxId, signature.Tenant, signature.Html, signature.IsActive);
        }
    }

    public static class SignatureEngineExtension
    {
        public static DIHelper AddSignatureEngineService(this DIHelper services)
        {
            services.TryAddScoped<SignatureEngine>();

            return services;
        }
    }
}
