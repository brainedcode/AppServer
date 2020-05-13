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
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Core.Users;
using ASC.Files.Core;
using ASC.Files.Core.Data;
using ASC.Files.Core.Security;
using ASC.Files.Resources;
using ASC.Web.Core.Files;
using ASC.Web.Core.Users;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Services.DocumentService;
using ASC.Web.Files.Services.NotifyService;
using ASC.Web.Files.Services.WCFService;

using Microsoft.Extensions.Options;

namespace ASC.Web.Files.Utils
{
    public class FileSharingAceHelper<T>
    {
        public FileSecurity FileSecurity { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public FileUtility FileUtility { get; }
        public UserManager UserManager { get; }
        public AuthContext AuthContext { get; }
        public DocumentServiceHelper DocumentServiceHelper { get; }
        public FileMarker FileMarker { get; }
        public NotifyClient NotifyClient { get; }
        public GlobalFolderHelper GlobalFolderHelper { get; }
        public FileSharingHelper FileSharingHelper { get; }

        public FileSharingAceHelper(
            FileSecurity fileSecurity,
            CoreBaseSettings coreBaseSettings,
            FileUtility fileUtility,
            UserManager userManager,
            AuthContext authContext,
            DocumentServiceHelper documentServiceHelper,
            FileMarker fileMarker,
            NotifyClient notifyClient,
            GlobalFolderHelper globalFolderHelper,
            FileSharingHelper fileSharingHelper)
        {
            FileSecurity = fileSecurity;
            CoreBaseSettings = coreBaseSettings;
            FileUtility = fileUtility;
            UserManager = userManager;
            AuthContext = authContext;
            DocumentServiceHelper = documentServiceHelper;
            FileMarker = fileMarker;
            NotifyClient = notifyClient;
            GlobalFolderHelper = globalFolderHelper;
            FileSharingHelper = fileSharingHelper;
        }

        public async Task<bool> SetAceObject(List<AceWrapper> aceWrappers, FileEntry<T> entry, bool notify, string message)
        {
            if (entry == null) throw new ArgumentNullException(FilesCommonResource.ErrorMassage_BadRequest);
            if (!await FileSharingHelper.CanSetAccess(entry)) throw new SecurityException(FilesCommonResource.ErrorMassage_SecurityException);

            var fileSecurity = FileSecurity;

            var entryType = entry.FileEntryType;
            var recipients = new Dictionary<Guid, FileShare>();
            var usersWithoutRight = new List<Guid>();
            var changed = false;

            foreach (var w in aceWrappers.OrderByDescending(ace => ace.SubjectGroup))
            {
                var subjects = fileSecurity.GetUserSubjects(w.SubjectId);

                var ownerId = entry.RootFolderType == FolderType.USER ? entry.RootFolderCreator : entry.CreateBy;
                if (entry.RootFolderType == FolderType.COMMON && subjects.Contains(Constants.GroupAdmin.ID)
                    || ownerId == w.SubjectId)
                    continue;

                var share = w.Share;

                if (w.SubjectId == FileConstant.ShareLinkId)
                {
                    if (w.Share == FileShare.ReadWrite && UserManager.GetUsers(AuthContext.CurrentAccount.ID).IsVisitor(UserManager)) throw new SecurityException(FilesCommonResource.ErrorMassage_SecurityException);
                    if (CoreBaseSettings.Personal && !FileUtility.CanWebView(entry.Title) && w.Share != FileShare.Restrict) throw new SecurityException(FilesCommonResource.ErrorMassage_BadRequest);
                    share = w.Share == FileShare.Restrict ? FileShare.None : w.Share;
                }

                fileSecurity.Share(entry.ID, entryType, w.SubjectId, share);
                changed = true;

                if (w.SubjectId == FileConstant.ShareLinkId)
                    continue;

                entry.Access = share;

                var listUsersId = new List<Guid>();

                if (w.SubjectGroup)
                    listUsersId = UserManager.GetUsersByGroup(w.SubjectId).Select(ui => ui.ID).ToList();
                else
                    listUsersId.Add(w.SubjectId);
                listUsersId.Remove(AuthContext.CurrentAccount.ID);

                if (entryType == FileEntryType.File)
                {
                    listUsersId.ForEach(uid => FileTracker.ChangeRight(entry.ID, uid, true));
                }

                var addRecipient = share == FileShare.Read
                                   || share == FileShare.ReadWrite
                                   || share == FileShare.Review
                                   || share == FileShare.FillForms
                                   || share == FileShare.Comment
                                   || share == FileShare.None && entry.RootFolderType == FolderType.COMMON;
                var removeNew = share == FileShare.None && entry.RootFolderType == FolderType.USER
                                || share == FileShare.Restrict;
                listUsersId.ForEach(id =>
                {
                    recipients.Remove(id);
                    if (addRecipient)
                    {
                        recipients.Add(id, share);
                    }
                    else if (removeNew)
                    {
                        usersWithoutRight.Add(id);
                    }
                });
            }

            if (entryType == FileEntryType.File)
            {
                await DocumentServiceHelper.CheckUsersForDrop((File<T>)entry);
            }

            if (recipients.Any())
            {
                if (entryType == FileEntryType.File
                    || ((Folder<T>)entry).TotalSubFolders + ((Folder<T>)entry).TotalFiles > 0
                    || entry.ProviderEntry)
                {
                    await FileMarker.MarkAsNew(entry, recipients.Keys.ToList());
                }

                if (entry.RootFolderType == FolderType.USER
                    && notify)
                {
                    NotifyClient.SendShareNotice(entry, recipients, message);
                }
            }

            usersWithoutRight.ForEach(async userId => await FileMarker.RemoveMarkAsNew(entry, userId));

            return changed;
        }

        public void RemoveAce(List<FileEntry<T>> entries)
        {
            var fileSecurity = FileSecurity;

            entries.ForEach(async
                entry =>
                {
                    if (entry.RootFolderType != FolderType.USER || Equals(entry.RootFolderId, GlobalFolderHelper.FolderMy))
                        return;

                    var entryType = entry.FileEntryType;
                    fileSecurity.Share(entry.ID, entryType, AuthContext.CurrentAccount.ID, fileSecurity.DefaultMyShare);

                    if (entryType == FileEntryType.File)
                    {
                        await DocumentServiceHelper.CheckUsersForDrop((File<T>)entry);
                    }

                    await FileMarker.RemoveMarkAsNew(entry);
                });
        }
    }

    public class FileSharingHelper
    {
        public FileSharingHelper(
            Global global,
            GlobalFolderHelper globalFolderHelper,
            FileSecurity fileSecurity,
            AuthContext authContext,
            UserManager userManager)
        {
            Global = global;
            GlobalFolderHelper = globalFolderHelper;
            FileSecurity = fileSecurity;
            AuthContext = authContext;
            UserManager = userManager;
        }

        public Global Global { get; }
        public GlobalFolderHelper GlobalFolderHelper { get; }
        public FileSecurity FileSecurity { get; }
        public AuthContext AuthContext { get; }
        public UserManager UserManager { get; }

        public async Task<bool> CanSetAccess<T>(FileEntry<T> entry)
        {
            return
                entry != null
                && (entry.RootFolderType == FolderType.COMMON && Global.IsAdministrator
                    || entry.RootFolderType == FolderType.USER
                    && (Equals(entry.RootFolderId, await GlobalFolderHelper.GetFolderMy<T>()) || await FileSecurity.CanEdit(entry))
                    && !UserManager.GetUsers(AuthContext.CurrentAccount.ID).IsVisitor(UserManager));
        }
    }

    public class FileSharing
    {
        public Global Global { get; }
        public FileSecurity FileSecurity { get; }
        public AuthContext AuthContext { get; }
        public UserManager UserManager { get; }
        public DisplayUserSettingsHelper DisplayUserSettingsHelper { get; }
        public FileShareLink FileShareLink { get; }
        public IDaoFactory DaoFactory { get; }
        public FileSharingHelper FileSharingHelper { get; }
        public ILog Logger { get; }

        public FileSharing(
            Global global,
            FileSecurity fileSecurity,
            AuthContext authContext,
            UserManager userManager,
            IOptionsMonitor<ILog> optionsMonitor,
            DisplayUserSettingsHelper displayUserSettingsHelper,
            FileShareLink fileShareLink,
            IDaoFactory daoFactory,
            FileSharingHelper fileSharingHelper)
        {
            Global = global;
            FileSecurity = fileSecurity;
            AuthContext = authContext;
            UserManager = userManager;
            DisplayUserSettingsHelper = displayUserSettingsHelper;
            FileShareLink = fileShareLink;
            DaoFactory = daoFactory;
            FileSharingHelper = fileSharingHelper;
            Logger = optionsMonitor.CurrentValue;
        }

        public async Task<bool> CanSetAccess<T>(FileEntry<T> entry)
        {
            return await FileSharingHelper.CanSetAccess(entry);
        }

        public async Task<List<AceWrapper>> GetSharedInfo<T>(FileEntry<T> entry)
        {
            if (entry == null) throw new ArgumentNullException(FilesCommonResource.ErrorMassage_BadRequest);
            if (!await CanSetAccess(entry))
            {
                Logger.ErrorFormat("User {0} can't get shared info for {1} {2}", AuthContext.CurrentAccount.ID, (entry.FileEntryType == FileEntryType.File ? "file" : "folder"), entry.ID);
                throw new SecurityException(FilesCommonResource.ErrorMassage_SecurityException);
            }

            var linkAccess = FileShare.Restrict;
            var result = new List<AceWrapper>();

            var fileSecurity = FileSecurity;

            var records = fileSecurity
                .GetShares(entry)
                .GroupBy(r => r.Subject)
                .Select(g => g.OrderBy(r => r.Level)
                              .ThenBy(r => r.Level)
                              .ThenByDescending(r => r.Share, new FileShareRecord.ShareComparer()).FirstOrDefault());

            foreach (var r in records)
            {
                if (r.Subject == FileConstant.ShareLinkId)
                {
                    linkAccess = r.Share;
                    continue;
                }

                var u = UserManager.GetUsers(r.Subject);
                var isgroup = false;
                var title = u.DisplayUserName(false, DisplayUserSettingsHelper);

                if (u.ID == Constants.LostUser.ID)
                {
                    var g = UserManager.GetGroupInfo(r.Subject);
                    isgroup = true;
                    title = g.Name;

                    if (g.ID == Constants.GroupAdmin.ID)
                        title = FilesCommonResource.Admin;
                    if (g.ID == Constants.GroupEveryone.ID)
                        title = FilesCommonResource.Everyone;

                    if (g.ID == Constants.LostGroupInfo.ID)
                    {
                        fileSecurity.RemoveSubject<T>(r.Subject);
                        continue;
                    }
                }

                var w = new AceWrapper
                {
                    SubjectId = r.Subject,
                    SubjectName = title,
                    SubjectGroup = isgroup,
                    Share = r.Share,
                    Owner =
                            entry.RootFolderType == FolderType.USER
                                ? entry.RootFolderCreator == r.Subject
                                : entry.CreateBy == r.Subject,
                    LockedRights = r.Subject == AuthContext.CurrentAccount.ID
                };
                result.Add(w);
            }

            if (entry.FileEntryType == FileEntryType.File && result.All(w => w.SubjectId != FileConstant.ShareLinkId)
                && entry.FileEntryType == FileEntryType.File
                && !((File<T>)entry).Encrypted)
            {
                var w = new AceWrapper
                {
                    SubjectId = FileConstant.ShareLinkId,
                    Link = FileShareLink.GetLink((File<T>)entry),
                    SubjectGroup = true,
                    Share = linkAccess,
                    Owner = false
                };
                result.Add(w);
            }

            if (!result.Any(w => w.Owner))
            {
                var ownerId = entry.RootFolderType == FolderType.USER ? entry.RootFolderCreator : entry.CreateBy;
                var w = new AceWrapper
                {
                    SubjectId = ownerId,
                    SubjectName = Global.GetUserName(ownerId),
                    SubjectGroup = false,
                    Share = FileShare.ReadWrite,
                    Owner = true
                };
                result.Add(w);
            }

            if (result.Any(w => w.SubjectId == AuthContext.CurrentAccount.ID))
            {
                result.Single(w => w.SubjectId == AuthContext.CurrentAccount.ID).LockedRights = true;
            }

            if (entry.RootFolderType == FolderType.COMMON)
            {
                if (result.All(w => w.SubjectId != Constants.GroupAdmin.ID))
                {
                    var w = new AceWrapper
                    {
                        SubjectId = Constants.GroupAdmin.ID,
                        SubjectName = FilesCommonResource.Admin,
                        SubjectGroup = true,
                        Share = FileShare.ReadWrite,
                        Owner = false,
                        LockedRights = true,
                    };
                    result.Add(w);
                }
                if (result.All(w => w.SubjectId != Constants.GroupEveryone.ID))
                {
                    var w = new AceWrapper
                    {
                        SubjectId = Constants.GroupEveryone.ID,
                        SubjectName = FilesCommonResource.Everyone,
                        SubjectGroup = true,
                        Share = fileSecurity.DefaultCommonShare,
                        Owner = false,
                        DisableRemove = true
                    };
                    result.Add(w);
                }
            }

            return result;
        }

        public async Task<ItemList<AceWrapper>> GetSharedInfo<T>(ItemList<string> objectIds)
        {
            if (!AuthContext.IsAuthenticated)
            {
                throw new InvalidOperationException(FilesCommonResource.ErrorMassage_SecurityException);
            }

            var result = new List<AceWrapper>();

            var folderDao = DaoFactory.GetFolderDao<T>();
            var fileDao = DaoFactory.GetFileDao<T>();

            foreach (var objectId in objectIds)
            {
                if (string.IsNullOrEmpty(objectId))
                {
                    throw new InvalidOperationException(FilesCommonResource.ErrorMassage_BadRequest);
                }

                var entryType = objectId.StartsWith("file_") ? FileEntryType.File : FileEntryType.Folder;
                var entryId = (T)Convert.ChangeType(objectId.Substring((entryType == FileEntryType.File ? "file_" : "folder_").Length), typeof(T));

                var entry = entryType == FileEntryType.File
                                ? fileDao.GetFile(entryId)
                                : (FileEntry<T>)await folderDao.GetFolder(entryId);

                IEnumerable<AceWrapper> acesForObject;
                try
                {
                    acesForObject = await GetSharedInfo(entry);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    throw new InvalidOperationException(e.Message, e);
                }

                foreach (var aceForObject in acesForObject)
                {
                    var duplicate = result.FirstOrDefault(ace => ace.SubjectId == aceForObject.SubjectId);
                    if (duplicate == null)
                    {
                        if (result.Any())
                        {
                            aceForObject.Owner = false;
                            aceForObject.Share = FileShare.Varies;
                        }
                        continue;
                    }

                    if (duplicate.Share != aceForObject.Share)
                    {
                        aceForObject.Share = FileShare.Varies;
                    }
                    if (duplicate.Owner != aceForObject.Owner)
                    {
                        aceForObject.Owner = false;
                        aceForObject.Share = FileShare.Varies;
                    }
                    result.Remove(duplicate);
                }

                var withoutAce = result.Where(ace =>
                                                acesForObject.FirstOrDefault(aceForObject =>
                                                                            aceForObject.SubjectId == ace.SubjectId) == null);
                foreach (var ace in withoutAce)
                {
                    ace.Share = FileShare.Varies;
                }

                var notOwner = result.Where(ace =>
                                            ace.Owner &&
                                            acesForObject.FirstOrDefault(aceForObject =>
                                                                            aceForObject.Owner
                                                                            && aceForObject.SubjectId == ace.SubjectId) == null);
                foreach (var ace in notOwner)
                {
                    ace.Owner = false;
                    ace.Share = FileShare.Varies;
                }

                result.AddRange(acesForObject);
            }


            var ownerAce = result.FirstOrDefault(ace => ace.Owner);
            result.Remove(ownerAce);

            var meAce = result.FirstOrDefault(ace => ace.SubjectId == AuthContext.CurrentAccount.ID);
            result.Remove(meAce);

            AceWrapper linkAce = null;
            if (objectIds.Count > 1)
            {
                result.RemoveAll(ace => ace.SubjectId == FileConstant.ShareLinkId);
            }
            else
            {
                linkAce = result.FirstOrDefault(ace => ace.SubjectId == FileConstant.ShareLinkId);
            }

            result.Sort((x, y) => string.Compare(x.SubjectName, y.SubjectName));

            if (ownerAce != null)
            {
                result = new List<AceWrapper> { ownerAce }.Concat(result).ToList();
            }
            if (meAce != null)
            {
                result = new List<AceWrapper> { meAce }.Concat(result).ToList();
            }
            if (linkAce != null)
            {
                result.Remove(linkAce);
                result = new List<AceWrapper> { linkAce }.Concat(result).ToList();
            }

            return new ItemList<AceWrapper>(result);
        }

        public async Task<ItemList<AceShortWrapper>> GetSharedInfoShort<T>(string objectId)
        {
            var aces = await GetSharedInfo<T>(new ItemList<string> { objectId });

            return new ItemList<AceShortWrapper>(
                aces.Where(aceWrapper => !aceWrapper.SubjectId.Equals(FileConstant.ShareLinkId) || aceWrapper.Share != FileShare.Restrict)
                    .Select(aceWrapper => new AceShortWrapper(aceWrapper)));
        }
    }

    public static class FileSharingExtension
    {
        public static DIHelper AddFileSharingService(this DIHelper services)
        {
            services.TryAddScoped<FileSharing>();

            return services
                .AddGlobalService()
                .AddFileSecurityService()
                .AddAuthContextService()
                .AddUserManagerService()
                .AddDisplayUserSettingsService()
                .AddFileShareLinkService()
                .AddDaoFactoryService()
                .AddFileSharingHelperService();
        }
        public static DIHelper AddFileSharingHelperService(this DIHelper services)
        {
            services.TryAddScoped<FileSharingHelper>();
            return services
                .AddGlobalService()
                .AddGlobalFolderHelperService()
                .AddFileSecurityService()
                .AddAuthContextService()
                .AddUserManagerService();
        }
        public static DIHelper AddFileSharingAceHelperService(this DIHelper services)
        {
            services.TryAddScoped<FileSharingAceHelper<string>>();
            services.TryAddScoped<FileSharingAceHelper<int>>();

            return services
                .AddFileSecurityService()
                .AddCoreBaseSettingsService()
                .AddFileUtilityService()
                .AddUserManagerService()
                .AddAuthContextService()
                .AddDocumentServiceHelperService()
                .AddFileMarkerService()
                .AddNotifyClientService()
                .AddGlobalFolderHelperService()
                .AddFileSharingHelperService();
        }
    }
}