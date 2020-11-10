﻿/*
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

using System;
using System.Collections.Generic;
using System.Linq;

using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.ElasticSearch;
using ASC.ElasticSearch.Core;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Core.EF;
using ASC.Files.Core.Resources;

using Microsoft.Extensions.Options;

namespace ASC.Web.Files.Core.Search
{
    public class FactoryIndexerFile : FactoryIndexer<DbFile>
    {
        private IDaoFactory DaoFactory { get; }

        public FactoryIndexerFile(
            IOptionsMonitor<ILog> options,
            TenantManager tenantManager,
            SearchSettingsHelper searchSettingsHelper,
            FactoryIndexer factoryIndexer,
            BaseIndexer<DbFile> baseIndexer,
            IServiceProvider serviceProvider,
            IDaoFactory daoFactory)
            : base(options, tenantManager, searchSettingsHelper, factoryIndexer, baseIndexer, serviceProvider)
        {
            DaoFactory = daoFactory;
        }

        public override void IndexAll()
        {
            var fileDao = DaoFactory.GetFileDao<int>() as FileDao;

            (int, int, int) getCount(DateTime lastIndexed)
            {
                var q = fileDao.FilesDbContext.Files
                    .Where(r => r.ModifiedOn >= lastIndexed)
                    .Where(r => r.CurrentVersion)
                    .Join(fileDao.FilesDbContext.Tenants, r => r.TenantId, r => r.Id, (f, t) => new { f, t })
                    .Where(r => r.t.Status == ASC.Core.Tenants.TenantStatus.Active);

                var count = q.GroupBy(a => a.f.Id).Count();
                var min = count > 0 ? q.Min(r => r.f.Id) : 0;
                var max = count > 0 ? q.Max(r => r.f.Id) : 0;

                return (count, max, min);
            }

            List<DbFile> getData(long i, long step, DateTime lastIndexed) =>
                fileDao.FilesDbContext.Files
                    .Where(r => r.ModifiedOn >= lastIndexed)
                    .Where(r => r.CurrentVersion)
                    .Where(r => r.Id >= i && r.Id <= i + step)
                    .Join(fileDao.FilesDbContext.Tenants, r => r.TenantId, r => r.Id, (f, t) => new { f, t })
                    .Where(r => r.t.Status == ASC.Core.Tenants.TenantStatus.Active)
                    .Select(r => r.f)
                    .ToList();

            try
            {
                foreach (var data in Indexer.IndexAll(getCount, getData))
                {
                    data.ForEach(r =>
                    {
                        TenantManager.SetCurrentTenant(r.TenantId);
                        fileDao.InitDocument(r);
                    });
                    Index(data);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
            }
        }


        public override string SettingsTitle
        {
            get { return FilesCommonResource.IndexTitle; }
        }
    }
    public static class FactoryIndexerFileExtention
    {
        public static DIHelper AddFactoryIndexerFileService(this DIHelper services)
        {
            if (services.TryAddScoped<FactoryIndexer<DbFile>, FactoryIndexerFile>())
            {
                services.TryAddTransient<DbFile>();

                return services
                    .AddFactoryIndexerService<DbFile>(false);
            }

            return services;
        }
    }
}
