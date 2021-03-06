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
using System.Reflection;
using System.Web;

using ASC.Common;
using ASC.Core;
using ASC.Core.Common.Settings;
using ASC.Core.Users;

using Microsoft.Extensions.Configuration;

namespace ASC.Web.Core.Users
{
    [Serializable]
    public class DisplayUserSettings : ISettings
    {
        public Guid ID
        {
            get { return new Guid("2EF59652-E1A7-4814-BF71-FEB990149428"); }
        }

        public bool IsDisableGettingStarted { get; set; }

        public ISettings GetDefault(IServiceProvider serviceProvider)
        {
            return new DisplayUserSettings
            {
                IsDisableGettingStarted = false,
            };
        }
    }

    [Scope]
    public class DisplayUserSettingsHelper
    {
        private readonly string RemovedProfileName;
        public DisplayUserSettingsHelper(UserManager userManager, UserFormatter userFormatter, IConfiguration configuration)
        {
            UserManager = userManager;
            UserFormatter = userFormatter;
            RemovedProfileName = configuration["web:removed-profile-name"] ?? "profile removed";
        }

        private UserManager UserManager { get; }
        private UserFormatter UserFormatter { get; }

        public string GetFullUserName(Guid userID, bool withHtmlEncode = true)
        {
            return GetFullUserName(UserManager.GetUsers(userID), withHtmlEncode);
        }

        public string GetFullUserName(UserInfo userInfo, bool withHtmlEncode = true)
        {
            return GetFullUserName(userInfo, DisplayUserNameFormat.Default, withHtmlEncode);
        }

        public string GetFullUserName(UserInfo userInfo, DisplayUserNameFormat format, bool withHtmlEncode)
        {
            if (userInfo == null)
            {
                return string.Empty;
            }
            if (!userInfo.ID.Equals(Guid.Empty) && !UserManager.UserExists(userInfo))
            {
                try
                {
                    var resourceType = Type.GetType("ASC.Web.Core.PublicResources.Resources.Resource, ASC.Web.Core");
                    var resourceProperty = resourceType.GetProperty("ProfileRemoved", BindingFlags.Static | BindingFlags.Public);
                    var resourceValue = (string)resourceProperty.GetValue(null);

                    return string.IsNullOrEmpty(resourceValue) ? RemovedProfileName : resourceValue;
                }
                catch (Exception)
                {
                    return RemovedProfileName;
                }
            }
            var result = UserFormatter.GetUserName(userInfo, format);
            return withHtmlEncode ? HtmlEncode(result) : result;
        }
        public string HtmlEncode(string str)
        {
            return !string.IsNullOrEmpty(str) ? HttpUtility.HtmlEncode(str) : str;
        }
    }
}