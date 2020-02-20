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


//using System;
//using System.Linq;
//using ASC.Common.Data;
//using ASC.Common.Data.Sql;
//using ASC.Mail.Core.Dao.Interfaces;
//using ASC.Mail.Core.DbSchema;
//using ASC.Mail.Core.DbSchema.Interfaces;
//using ASC.Mail.Core.DbSchema.Tables;
//using ASC.Mail.Core.Entities;

using System.Linq;
using ASC.Api.Core;
using ASC.Core;
using ASC.Core.Common.EF;
using ASC.Mail.Core.Dao.Entities;
using ASC.Mail.Core.Dao.Interfaces;
using ASC.Mail.Core.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ASC.Mail.Core.Dao
{
    public class MailboxDomainDao : BaseDao, IMailboxDomainDao
    {
        public MailboxDomainDao(ApiContext apiContext,
            SecurityContext securityContext,
            DbContextManager<MailDbContext> dbContext)
            : base(apiContext, securityContext, dbContext)
        {
        }

        public MailboxDomain GetDomain(string domainName)
        {
            var domain = MailDb.MailMailboxDomain
                .Where(d => d.Name == domainName)
                .Select(ToMailboxDomain)
                .FirstOrDefault();

            return domain;
        }

        public int SaveDomain(MailboxDomain domain)
        {
            var mailboxDomain = new MailMailboxDomain
            {
                Id = domain.Id,
                IdProvider = domain.ProviderId,
                Name = domain.Name
            };

            var result = MailDb.MailMailboxDomain.Add(mailboxDomain).Entity;

            MailDb.SaveChanges();

            return result.Id;
        }

        protected MailboxDomain ToMailboxDomain(MailMailboxDomain r)
        {
            var d = new MailboxDomain
            {
                Id = r.Id,
                ProviderId = r.IdProvider,
                Name = r.Name
            };

            return d;
        }
    }

    public static class MailboxDomainDaoExtension
    {
        public static IServiceCollection AddMailboxDomainDaoService(this IServiceCollection services)
        {
            services.TryAddScoped<MailboxDomain>();

            return services;
        }
    }
}