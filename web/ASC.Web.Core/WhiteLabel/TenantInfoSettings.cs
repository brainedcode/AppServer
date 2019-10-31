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


using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Data.Storage;
using ASC.Web.Core.Utility.Skins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ASC.Web.Core.WhiteLabel
{
    [Serializable]
    [DataContract]
    public class TenantInfoSettings : BaseSettings<TenantInfoSettings>
    {
        [DataMember(Name = "LogoSize")]
        public Size CompanyLogoSize { get; private set; }

        [DataMember(Name = "LogoFileName")] private string _companyLogoFileName;

        [DataMember(Name = "Default")]
        private bool _isDefault { get; set; }

        public TenantInfoSettings()
        {
        }
        public TenantInfoSettings(
            AuthContext authContext,
            SettingsManager settingsManager,
            WebImageSupplier webImageSupplier,
            TenantManager tenantManager,
            StorageFactory storageFactory,
            IConfiguration configuration) : base(authContext, settingsManager, tenantManager)
        {
            WebImageSupplier = webImageSupplier;
            StorageFactory = storageFactory;
            Configuration = configuration;
        }

        #region ISettings Members

        public override ISettings GetDefault()
        {
            return new TenantInfoSettings()
            {
                _isDefault = true
            };
        }

        public void RestoreDefault(TenantLogoManager tenantLogoManager)
        {
            RestoreDefaultTenantName();
            RestoreDefaultLogo(tenantLogoManager);
        }

        public void RestoreDefaultTenantName()
        {
            var currentTenant = TenantManager.GetCurrentTenant();
            currentTenant.Name = Configuration["web:portal-name"] ?? "Cloud Office Applications";
            TenantManager.SaveTenant(currentTenant);
        }

        public void RestoreDefaultLogo(TenantLogoManager tenantLogoManager)
        {
            _isDefault = true;

            var store = StorageFactory.GetStorage(TenantManager.GetCurrentTenant().TenantId.ToString(), "logo");
            try
            {
                store.DeleteFiles("", "*", false);
            }
            catch
            {
            }
            CompanyLogoSize = default;

            tenantLogoManager.RemoveMailLogoDataFromCache();
        }

        public void SetCompanyLogo(string companyLogoFileName, byte[] data, TenantLogoManager tenantLogoManager)
        {
            var store = StorageFactory.GetStorage(TenantManager.GetCurrentTenant().TenantId.ToString(), "logo");

            if (!_isDefault)
            {
                try
                {
                    store.DeleteFiles("", "*", false);
                }
                catch
                {
                }
            }
            using (var memory = new MemoryStream(data))
            using (var image = Image.FromStream(memory))
            {
                CompanyLogoSize = image.Size;
                memory.Seek(0, SeekOrigin.Begin);
                store.Save(companyLogoFileName, memory);
                _companyLogoFileName = companyLogoFileName;
            }
            _isDefault = false;

            tenantLogoManager.RemoveMailLogoDataFromCache();
        }

        public string GetAbsoluteCompanyLogoPath()
        {
            if (_isDefault)
            {
                return WebImageSupplier.GetAbsoluteWebPath("onlyoffice_logo/dark_general.png");
            }

            var store = StorageFactory.GetStorage(TenantManager.GetCurrentTenant().TenantId.ToString(), "logo");
            return store.GetUri(_companyLogoFileName ?? "").ToString();
        }

        /// <summary>
        /// Get logo stream or null in case of default logo
        /// </summary>
        public Stream GetStorageLogoData()
        {
            if (_isDefault) return null;

            var storage = StorageFactory.GetStorage(TenantManager.GetCurrentTenant().TenantId.ToString(CultureInfo.InvariantCulture), "logo");

            if (storage == null) return null;

            var fileName = _companyLogoFileName ?? "";

            return storage.IsFile(fileName) ? storage.GetReadStream(fileName) : null;
        }

        public override Guid ID
        {
            get { return new Guid("{5116B892-CCDD-4406-98CD-4F18297C0C0A}"); }
        }

        public WebImageSupplier WebImageSupplier { get; }
        public StorageFactory StorageFactory { get; }
        public IConfiguration Configuration { get; }

        #endregion
    }

    public static class TenantInfoSettingsFactory
    {
        public static IServiceCollection AddTenantInfoSettingsService(this IServiceCollection services)
        {
            return services
                .AddWebImageSupplierService()
                .AddStorageFactoryService()
                .AddSettingsService<TenantInfoSettings>();
        }
    }
}