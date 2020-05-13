﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using ASC.Api.Core;
using ASC.Api.Documents;
using ASC.Api.Utils;
using ASC.Common;
using ASC.Common.Web;
using ASC.Core;
using ASC.Core.Common.Configuration;
using ASC.Core.Users;
using ASC.FederatedLogin.Helpers;
using ASC.FederatedLogin.LoginProviders;
using ASC.Files.Core;
using ASC.Files.Model;
using ASC.MessagingSystem;
using ASC.Web.Core;
using ASC.Web.Core.Files;
using ASC.Web.Files.Classes;
using ASC.Web.Files.Configuration;
using ASC.Web.Files.Helpers;
using ASC.Web.Files.Services.DocumentService;
using ASC.Web.Files.Services.WCFService;
using ASC.Web.Files.Utils;
using ASC.Web.Studio.Utility;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

using static ASC.Api.Documents.FilesController;

using FileShare = ASC.Files.Core.Security.FileShare;
using MimeMapping = ASC.Common.Web.MimeMapping;
using SortedByType = ASC.Files.Core.SortedByType;

namespace ASC.Files.Helpers
{
    public class FilesControllerHelper<T>
    {
        private readonly ApiContext ApiContext;
        private readonly FileStorageService<T> FileStorageService;

        public GlobalFolderHelper GlobalFolderHelper { get; }
        public FileWrapperHelper FileWrapperHelper { get; }
        public FilesSettingsHelper FilesSettingsHelper { get; }
        public FilesLinkUtility FilesLinkUtility { get; }
        public FileUploader FileUploader { get; }
        public DocumentServiceHelper DocumentServiceHelper { get; }
        public TenantManager TenantManager { get; }
        public SecurityContext SecurityContext { get; }
        public FolderWrapperHelper FolderWrapperHelper { get; }
        public FileOperationWraperHelper FileOperationWraperHelper { get; }
        public FileShareWrapperHelper FileShareWrapperHelper { get; }
        public FileShareParamsHelper FileShareParamsHelper { get; }
        public EntryManager EntryManager { get; }
        public UserManager UserManager { get; }
        public WebItemSecurity WebItemSecurity { get; }
        public CoreBaseSettings CoreBaseSettings { get; }
        public ThirdpartyConfiguration ThirdpartyConfiguration { get; }
        public BoxLoginProvider BoxLoginProvider { get; }
        public DropboxLoginProvider DropboxLoginProvider { get; }
        public GoogleLoginProvider GoogleLoginProvider { get; }
        public OneDriveLoginProvider OneDriveLoginProvider { get; }
        public MessageService MessageService { get; }
        public CommonLinkUtility CommonLinkUtility { get; }
        public DocumentServiceConnector DocumentServiceConnector { get; }
        public FolderContentWrapperHelper FolderContentWrapperHelper { get; }
        public WordpressToken WordpressToken { get; }
        public WordpressHelper WordpressHelper { get; }
        public ConsumerFactory ConsumerFactory { get; }
        public EasyBibHelper EasyBibHelper { get; }
        public ChunkedUploadSessionHelper ChunkedUploadSessionHelper { get; }
        public ProductEntryPoint ProductEntryPoint { get; }

        /// <summary>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="fileStorageService"></param>
        public FilesControllerHelper(
            ApiContext context,
            FileStorageService<T> fileStorageService,
            GlobalFolderHelper globalFolderHelper,
            FileWrapperHelper fileWrapperHelper,
            FilesSettingsHelper filesSettingsHelper,
            FilesLinkUtility filesLinkUtility,
            FileUploader fileUploader,
            DocumentServiceHelper documentServiceHelper,
            TenantManager tenantManager,
            SecurityContext securityContext,
            FolderWrapperHelper folderWrapperHelper,
            FileOperationWraperHelper fileOperationWraperHelper,
            FileShareWrapperHelper fileShareWrapperHelper,
            FileShareParamsHelper fileShareParamsHelper,
            EntryManager entryManager,
            UserManager userManager,
            WebItemSecurity webItemSecurity,
            CoreBaseSettings coreBaseSettings,
            ThirdpartyConfiguration thirdpartyConfiguration,
            MessageService messageService,
            CommonLinkUtility commonLinkUtility,
            DocumentServiceConnector documentServiceConnector,
            FolderContentWrapperHelper folderContentWrapperHelper,
            WordpressToken wordpressToken,
            WordpressHelper wordpressHelper,
            ConsumerFactory consumerFactory,
            EasyBibHelper easyBibHelper,
            ChunkedUploadSessionHelper chunkedUploadSessionHelper,
            ProductEntryPoint productEntryPoint)
        {
            ApiContext = context;
            FileStorageService = fileStorageService;
            GlobalFolderHelper = globalFolderHelper;
            FileWrapperHelper = fileWrapperHelper;
            FilesSettingsHelper = filesSettingsHelper;
            FilesLinkUtility = filesLinkUtility;
            FileUploader = fileUploader;
            DocumentServiceHelper = documentServiceHelper;
            TenantManager = tenantManager;
            SecurityContext = securityContext;
            FolderWrapperHelper = folderWrapperHelper;
            FileOperationWraperHelper = fileOperationWraperHelper;
            FileShareWrapperHelper = fileShareWrapperHelper;
            FileShareParamsHelper = fileShareParamsHelper;
            EntryManager = entryManager;
            UserManager = userManager;
            WebItemSecurity = webItemSecurity;
            CoreBaseSettings = coreBaseSettings;
            ThirdpartyConfiguration = thirdpartyConfiguration;
            ConsumerFactory = consumerFactory;
            BoxLoginProvider = ConsumerFactory.Get<BoxLoginProvider>();
            DropboxLoginProvider = ConsumerFactory.Get<DropboxLoginProvider>();
            GoogleLoginProvider = ConsumerFactory.Get<GoogleLoginProvider>();
            OneDriveLoginProvider = ConsumerFactory.Get<OneDriveLoginProvider>();
            MessageService = messageService;
            CommonLinkUtility = commonLinkUtility;
            DocumentServiceConnector = documentServiceConnector;
            FolderContentWrapperHelper = folderContentWrapperHelper;
            WordpressToken = wordpressToken;
            WordpressHelper = wordpressHelper;
            EasyBibHelper = easyBibHelper;
            ChunkedUploadSessionHelper = chunkedUploadSessionHelper;
            ProductEntryPoint = productEntryPoint;
        }

        public async Task<FolderContentWrapper<T>> GetFolder(T folderId, Guid userIdOrGroupId, FilterType filterType, bool withSubFolders)
        {
            return await ToFolderContentWrapper(folderId, userIdOrGroupId, filterType, withSubFolders).NotFoundIfNull();
        }

        public async Task<List<FileWrapper<T>>> UploadFile(T folderId, UploadModel uploadModel)
        {
            if (uploadModel.StoreOriginalFileFlag.HasValue)
            {
                FilesSettingsHelper.StoreOriginalFiles = uploadModel.StoreOriginalFileFlag.Value;
            }

            if (uploadModel.Files != null && uploadModel.Files.Any())
            {
                if (uploadModel.Files.Count() == 1)
                {
                    //Only one file. return it
                    var postedFile = uploadModel.Files.First();
                    return new List<FileWrapper<T>>
                    {
                        await InsertFile(folderId, postedFile.OpenReadStream(), postedFile.FileName, uploadModel.CreateNewIfExist, uploadModel.KeepConvertStatus)
                    };
                }
                //For case with multiple files
                return (await Task.WhenAll(uploadModel.Files.Select(postedFile => InsertFile(folderId, postedFile.OpenReadStream(), postedFile.FileName, uploadModel.CreateNewIfExist, uploadModel.KeepConvertStatus)))).ToList();
            }
            if (uploadModel.File != null)
            {
                var fileName = "file" + MimeMapping.GetExtention(uploadModel.ContentType.MediaType);
                if (uploadModel.ContentDisposition != null)
                {
                    fileName = uploadModel.ContentDisposition.FileName;
                }

                return new List<FileWrapper<T>>
                {
                    await InsertFile(folderId, uploadModel.File, fileName, uploadModel.CreateNewIfExist, uploadModel.KeepConvertStatus)
                };
            }
            throw new InvalidOperationException("No input files");
        }

        public async Task<FileWrapper<T>> InsertFile(T folderId, Stream file, string title, bool? createNewIfExist, bool keepConvertStatus = false)
        {
            try
            {
                var resultFile = await FileUploader.Exec(folderId, title, file.Length, file, createNewIfExist ?? !FilesSettingsHelper.UpdateIfExist, !keepConvertStatus);
                return await FileWrapperHelper.Get(resultFile);
            }
            catch (FileNotFoundException e)
            {
                throw new ItemNotFoundException("File not found", e);
            }
            catch (DirectoryNotFoundException e)
            {
                throw new ItemNotFoundException("Folder not found", e);
            }
        }

        public async Task<FileWrapper<T>> UpdateFileStream(Stream file, T fileId, bool encrypted = false)
        {
            try
            {
                var resultFile = await FileStorageService.UpdateFileStream(fileId, file, encrypted);
                return await FileWrapperHelper.Get(resultFile);
            }
            catch (FileNotFoundException e)
            {
                throw new ItemNotFoundException("File not found", e);
            }
        }

        public async Task<FileWrapper<T>> SaveEditing(T fileId, string fileExtension, string downloadUri, Stream stream, string doc, bool forcesave)
        {
            return await FileWrapperHelper.Get(await FileStorageService.SaveEditing(fileId, fileExtension, downloadUri, stream, doc, forcesave));
        }

        public async Task<string> StartEdit(T fileId, bool editingAlone, string doc)
        {
            return await FileStorageService.StartEdit(fileId, editingAlone, doc);
        }

        public async Task<KeyValuePair<bool, string>> TrackEditFile(T fileId, Guid tabId, string docKeyForTrack, string doc, bool isFinish)
        {
            return await FileStorageService.TrackEditFile(fileId, tabId, docKeyForTrack, doc, isFinish);
        }

        public async Task<Configuration<T>> OpenEdit(T fileId, int version, string doc)
        {
            var (_, configuration) = await DocumentServiceHelper.GetParams(fileId, version, doc, true, true, true);
            configuration.Type = EditorType.External;
            configuration.Token = DocumentServiceHelper.GetSignature(configuration);
            return configuration;
        }

        public async Task<object> CreateUploadSession(T folderId, string fileName, long fileSize, string relativePath, bool encrypted)
        {
            var file = await FileUploader.VerifyChunkedUpload(folderId, fileName, fileSize, FilesSettingsHelper.UpdateIfExist, relativePath);

            if (FilesLinkUtility.IsLocalFileUploader)
            {
                var session = await FileUploader.InitiateUpload(file.FolderID, (file.ID ?? default), file.Title, file.ContentLength, encrypted);

                var responseObject = await ChunkedUploadSessionHelper.ToResponseObject(session, true);
                return new
                {
                    success = true,
                    data = responseObject
                };
            }

            var createSessionUrl = FilesLinkUtility.GetInitiateUploadSessionUrl(TenantManager.GetCurrentTenant().TenantId, file.FolderID, file.ID, file.Title, file.ContentLength, encrypted, SecurityContext);
            var request = (HttpWebRequest)WebRequest.Create(createSessionUrl);
            request.Method = "POST";
            request.ContentLength = 0;

            // hack for uploader.onlyoffice.com in api requests
            var rewriterHeader = ApiContext.HttpContext.Request.Headers[HttpRequestExtensions.UrlRewriterHeader];
            if (!string.IsNullOrEmpty(rewriterHeader))
            {
                request.Headers[HttpRequestExtensions.UrlRewriterHeader] = rewriterHeader;
            }

            // hack. http://ubuntuforums.org/showthread.php?t=1841740
            if (WorkContext.IsMono)
            {
                ServicePointManager.ServerCertificateValidationCallback += (s, ce, ca, p) => true;
            }

            using var response = request.GetResponse();
            using var responseStream = response.GetResponseStream();
            using var streamReader = new StreamReader(responseStream);
            return JObject.Parse(streamReader.ReadToEnd()); //result is json string
        }

        public async Task<FileWrapper<T>> CreateTextFile(T folderId, string title, string content)
        {
            if (title == null) throw new ArgumentNullException("title");
            //Try detect content
            var extension = ".txt";
            if (!string.IsNullOrEmpty(content))
            {
                if (Regex.IsMatch(content, @"<([^\s>]*)(\s[^<]*)>"))
                {
                    extension = ".html";
                }
            }
            return await CreateFile(folderId, title, content, extension);
        }

        private async Task<FileWrapper<T>> CreateFile(T folderId, string title, string content, string extension)
        {
            using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                var file = await FileUploader.Exec(folderId,
                                  title.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? title : (title + extension),
                                  memStream.Length, memStream);
                return await FileWrapperHelper.Get(file);
            }
        }

        public async Task<FileWrapper<T>> CreateHtmlFile(T folderId, string title, string content)
        {
            if (title == null) throw new ArgumentNullException("title");
            return await CreateFile(folderId, title, content, ".html");
        }

        public async Task<FolderWrapper<T>> CreateFolder(T folderId, string title)
        {
            var folder = await FileStorageService.CreateNewFolder(folderId, title);
            return await FolderWrapperHelper.Get(folder);
        }

        public async Task<FileWrapper<T>> CreateFile(T folderId, string title)
        {
            var file = await FileStorageService.CreateNewFile(new FileModel<T> { ParentId = folderId, Title = title });
            return await FileWrapperHelper.Get(file);
        }

        public async Task<FolderWrapper<T>> RenameFolder(T folderId, string title)
        {
            var folder = await FileStorageService.FolderRename(folderId, title);
            return await FolderWrapperHelper.Get(folder);
        }

        public async Task<FolderWrapper<T>> GetFolderInfo(T folderId)
        {
            var folder = await FileStorageService.GetFolder(folderId).NotFoundIfNull("Folder not found");

            return await FolderWrapperHelper.Get(folder);
        }

        public async Task<IEnumerable<FolderWrapper<T>>> GetFolderPath(T folderId)
        {
            var result = new List<FolderWrapper<T>>();
            var breadCrumbs = await EntryManager.GetBreadCrumbs(folderId);

            foreach (var b in breadCrumbs)
            {
                result.Add(await FolderWrapperHelper.Get(b));
            }

            return result;
        }

        public async Task<FileWrapper<T>> GetFileInfo(T fileId, int version = -1)
        {
            var file = await FileStorageService.GetFile(fileId, version).NotFoundIfNull("File not found");
            return await FileWrapperHelper.Get(file);
        }

        public async Task<FileWrapper<T>> UpdateFile(T fileId, string title, int lastVersion)
        {
            if (!string.IsNullOrEmpty(title))
                await FileStorageService.FileRename(fileId, title);

            if (lastVersion > 0)
                await FileStorageService.UpdateToVersion(fileId, lastVersion);

            return await GetFileInfo(fileId);
        }

        public async Task<IEnumerable<FileOperationWraper>> DeleteFile(T fileId, bool deleteAfter, bool immediately)
        {
            return await Task.WhenAll(FileStorageService.DeleteFile("delete", fileId, false, deleteAfter, immediately)
                .Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<ConversationResult<T>>> StartConversion(T fileId)
        {
            return await CheckConversion(fileId, true);
        }

        public async Task<IEnumerable<ConversationResult<T>>> CheckConversion(T fileId, bool start)
        {
            var data = (await FileStorageService.CheckConversion(new ItemList<ItemList<string>>
            {
                new ItemList<string> { fileId.ToString(), "0", start.ToString() }
            }));

            return await Task.WhenAll(data
            .Select(async r =>
            {
                var o = new ConversationResult<T>
                {
                    Id = r.Id,
                    Error = r.Error,
                    OperationType = r.OperationType,
                    Processed = r.Processed,
                    Progress = r.Progress,
                    Source = r.Source,
                };
                if (!string.IsNullOrEmpty(r.Result))
                {
                    var jResult = JObject.Parse(r.Result);
                    o.File = await GetFileInfo(jResult.Value<T>("id"), jResult.Value<int>("version"));
                }
                return o;
            }));
        }

        public async Task<IEnumerable<FileOperationWraper>> DeleteFolder(T folderId, bool deleteAfter, bool immediately)
        {
            return await Task.WhenAll(FileStorageService.DeleteFile("delete", folderId, false, deleteAfter, immediately)
                    .Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileEntryWrapper>> MoveOrCopyBatchCheck(BatchModel batchModel)
        {
            var (checkedFiles, checkedFolders) = await FileStorageService.MoveOrCopyFilesCheck(batchModel.FileIds, batchModel.FolderIds, batchModel.DestFolderId);

            var entries = await FileStorageService.GetItems(checkedFiles.OfType<long>().Select(Convert.ToInt32), checkedFiles.OfType<long>().Select(Convert.ToInt32), FilterType.FilesOnly, false, "", "");

            entries.AddRange(await FileStorageService.GetItems(checkedFiles.OfType<string>(), checkedFiles.OfType<string>(), FilterType.FilesOnly, false, "", ""));

            return await Task.WhenAll(entries.Select(async r =>
            {
                FileEntryWrapper wrapper = null;
                if (r is Folder<int> fol1)
                {
                    wrapper = await FolderWrapperHelper.Get(fol1);
                }
                if (r is Folder<string> fol2)
                {
                    wrapper = await FolderWrapperHelper.Get(fol2);
                }

                return wrapper;
            }));
        }

        public async Task<IEnumerable<FileOperationWraper>> MoveBatchItems(BatchModel batchModel)
        {
            return await Task.WhenAll(FileStorageService.MoveOrCopyItems(batchModel.FolderIds, batchModel.FileIds, batchModel.DestFolderId, batchModel.ConflictResolveType, false, batchModel.DeleteAfter)
                .Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> CopyBatchItems(BatchModel batchModel)
        {
            return await Task.WhenAll(FileStorageService.MoveOrCopyItems(batchModel.FolderIds, batchModel.FileIds, batchModel.DestFolderId, batchModel.ConflictResolveType, true, batchModel.DeleteAfter)
                .Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> MarkAsRead(BaseBatchModel<object> model)
        {
            return await Task.WhenAll(FileStorageService.MarkAsRead(model.FolderIds, model.FileIds).Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> TerminateTasks()
        {
            return await Task.WhenAll(FileStorageService.TerminateTasks().Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> GetOperationStatuses()
        {
            return await Task.WhenAll(FileStorageService.GetTasksStatuses().Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> BulkDownload(DownloadModel model)
        {
            var folders = new Dictionary<object, string>();
            var files = new Dictionary<object, string>();

            foreach (var fileId in model.FileConvertIds.Where(fileId => !files.ContainsKey(fileId.Key)))
            {
                files.Add(fileId.Key, fileId.Value);
            }

            foreach (var fileId in model.FileIds.Where(fileId => !files.ContainsKey(fileId)))
            {
                files.Add(fileId, string.Empty);
            }

            foreach (var folderId in model.FolderIds.Where(folderId => !folders.ContainsKey(folderId)))
            {
                folders.Add(folderId, string.Empty);
            }

            return await Task.WhenAll(FileStorageService.BulkDownload(folders, files).Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileOperationWraper>> EmptyTrash()
        {
            return await Task.WhenAll((await FileStorageService.EmptyTrash()).Select(FileOperationWraperHelper.Get));
        }

        public async Task<IEnumerable<FileWrapper<T>>> GetFileVersionInfo(T fileId)
        {
            var files = await FileStorageService.GetFileHistory(fileId);
            return await Task.WhenAll(files.Select(FileWrapperHelper.Get));
        }

        public async Task<IEnumerable<FileWrapper<T>>> ChangeHistory(T fileId, int version, bool continueVersion)
        {
            var history = (await FileStorageService.CompleteVersion(fileId, version, continueVersion)).Value;
            return await Task.WhenAll(history.Select(FileWrapperHelper.Get));
        }


        public async Task<IEnumerable<FileShareWrapper>> GetFileSecurityInfo(T fileId)
        {
            var fileShares = await FileStorageService.GetSharedInfo(new ItemList<string> { string.Format("file_{0}", fileId) });
            return fileShares.Select(FileShareWrapperHelper.Get);
        }

        public async Task<IEnumerable<FileShareWrapper>> GetFolderSecurityInfo(T folderId)
        {
            var fileShares = await FileStorageService.GetSharedInfo(new ItemList<string> { string.Format("folder_{0}", folderId) });
            return fileShares.Select(FileShareWrapperHelper.Get);
        }

        public async Task<IEnumerable<FileShareWrapper>> SetFileSecurityInfo(T fileId, IEnumerable<FileShareParams> share, bool notify, string sharingMessage)
        {
            if (share != null && share.Any())
            {
                var list = new ItemList<AceWrapper>(share.Select(FileShareParamsHelper.ToAceObject));
                var aceCollection = new AceCollection
                {
                    Entries = new ItemList<string> { "file_" + fileId },
                    Aces = list,
                    Message = sharingMessage
                };
                await FileStorageService.SetAceObject(aceCollection, notify);
            }
            return await GetFileSecurityInfo(fileId);
        }

        public async Task<IEnumerable<FileShareWrapper>> SetFolderSecurityInfo(T folderId, IEnumerable<FileShareParams> share, bool notify, string sharingMessage)
        {
            if (share != null && share.Any())
            {
                var list = new ItemList<AceWrapper>(share.Select(FileShareParamsHelper.ToAceObject));
                var aceCollection = new AceCollection
                {
                    Entries = new ItemList<string> { "folder_" + folderId },
                    Aces = list,
                    Message = sharingMessage
                };
                await FileStorageService.SetAceObject(aceCollection, notify);
            }

            return await GetFolderSecurityInfo(folderId);
        }

        public async Task<bool> RemoveSecurityInfo(BaseBatchModel<T> model)
        {
            var itemList = new ItemList<string>();

            itemList.AddRange((model.FolderIds ?? new List<T>()).Select(x => "folder_" + x));
            itemList.AddRange((model.FileIds ?? new List<T>()).Select(x => "file_" + x));

            await FileStorageService.RemoveAce(itemList);

            return true;
        }

        public async Task<string> GenerateSharedLink(T fileId, FileShare share)
        {
            var file = GetFileInfo(fileId);

            var objectId = "file_" + file.Id;
            var sharedInfo = (await FileStorageService.GetSharedInfo(new ItemList<string> { objectId })).Find(r => r.SubjectId == FileConstant.ShareLinkId);
            if (sharedInfo == null || sharedInfo.Share != share)
            {
                var list = new ItemList<AceWrapper>
                    {
                        new AceWrapper
                            {
                                SubjectId = FileConstant.ShareLinkId,
                                SubjectGroup = true,
                                Share = share
                            }
                    };
                var aceCollection = new AceCollection
                {
                    Entries = new ItemList<string> { objectId },
                    Aces = list
                };
                await FileStorageService.SetAceObject(aceCollection, false);
                sharedInfo = (await FileStorageService.GetSharedInfo(new ItemList<string> { objectId })).Find(r => r.SubjectId == FileConstant.ShareLinkId);
            }

            return sharedInfo.Link;
        }

        public List<List<string>> Capabilities()
        {
            var result = new List<List<string>>();

            if (UserManager.GetUsers(SecurityContext.CurrentAccount.ID).IsVisitor(UserManager)
                || (!UserManager.IsUserInGroup(SecurityContext.CurrentAccount.ID, Constants.GroupAdmin.ID)
                    && !WebItemSecurity.IsProductAdministrator(ProductEntryPoint.ID, SecurityContext.CurrentAccount.ID)
                    && !FilesSettingsHelper.EnableThirdParty
                    && !CoreBaseSettings.Personal))
            {
                return result;
            }

            if (ThirdpartyConfiguration.SupportBoxInclusion)
            {
                result.Add(new List<string> { "Box", BoxLoginProvider.ClientID, BoxLoginProvider.RedirectUri });
            }
            if (ThirdpartyConfiguration.SupportDropboxInclusion)
            {
                result.Add(new List<string> { "DropboxV2", DropboxLoginProvider.ClientID, DropboxLoginProvider.RedirectUri });
            }
            if (ThirdpartyConfiguration.SupportGoogleDriveInclusion)
            {
                result.Add(new List<string> { "GoogleDrive", GoogleLoginProvider.ClientID, GoogleLoginProvider.RedirectUri });
            }
            if (ThirdpartyConfiguration.SupportOneDriveInclusion)
            {
                result.Add(new List<string> { "OneDrive", OneDriveLoginProvider.ClientID, OneDriveLoginProvider.RedirectUri });
            }
            if (ThirdpartyConfiguration.SupportSharePointInclusion)
            {
                result.Add(new List<string> { "SharePoint" });
            }
            if (ThirdpartyConfiguration.SupportYandexInclusion)
            {
                result.Add(new List<string> { "Yandex" });
            }
            if (ThirdpartyConfiguration.SupportWebDavInclusion)
            {
                result.Add(new List<string> { "WebDav" });
            }

            //Obsolete BoxNet, DropBox, Google, SkyDrive,

            return result;
        }

        public async Task<FolderWrapper<T>> SaveThirdParty(
            string url,
            string login,
            string password,
            string token,
            bool isCorporate,
            string customerTitle,
            string providerKey,
            string providerId)
        {
            var thirdPartyParams = new ThirdPartyParams
            {
                AuthData = new AuthData(url, login, password, token),
                Corporate = isCorporate,
                CustomerTitle = customerTitle,
                ProviderId = providerId,
                ProviderKey = providerKey,
            };

            var folder = await FileStorageService.SaveThirdParty(thirdPartyParams);

            return await FolderWrapperHelper.Get(folder);
        }

        public IEnumerable<ThirdPartyParams> GetThirdPartyAccounts()
        {
            return FileStorageService.GetThirdParty();
        }

        public object DeleteThirdParty(int providerId)
        {
            return FileStorageService.DeleteThirdParty(providerId.ToString(CultureInfo.InvariantCulture));

        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="query"></param>
        ///// <returns></returns>
        //[Read(@"@search/{query}")]
        //public IEnumerable<FileEntryWrapper> Search(string query)
        //{
        //    var searcher = new SearchHandler();
        //    var files = searcher.SearchFiles(query).Select(r => (FileEntryWrapper)FileWrapperHelper.Get(r));
        //    var folders = searcher.SearchFolders(query).Select(f => (FileEntryWrapper)FolderWrapperHelper.Get(f));

        //    return files.Concat(folders);
        //}

        public bool StoreOriginal(bool set)
        {
            return FileStorageService.StoreOriginal(set);
        }

        public bool HideConfirmConvert(bool save)
        {
            return FileStorageService.HideConfirmConvert(save);
        }

        public bool UpdateIfExist(bool set)
        {
            return FileStorageService.UpdateIfExist(set);
        }

        public IEnumerable<string> CheckDocServiceUrl(string docServiceUrl, string docServiceUrlInternal, string docServiceUrlPortal)
        {
            FilesLinkUtility.DocServiceUrl = docServiceUrl;
            FilesLinkUtility.DocServiceUrlInternal = docServiceUrlInternal;
            FilesLinkUtility.DocServicePortalUrl = docServiceUrlPortal;

            MessageService.Send(MessageAction.DocumentServiceLocationSetting);

            var https = new Regex(@"^https://", RegexOptions.IgnoreCase);
            var http = new Regex(@"^http://", RegexOptions.IgnoreCase);
            if (https.IsMatch(CommonLinkUtility.GetFullAbsolutePath("")) && http.IsMatch(FilesLinkUtility.DocServiceUrl))
            {
                throw new Exception("Mixed Active Content is not allowed. HTTPS address for Document Server is required.");
            }

            DocumentServiceConnector.CheckDocServiceUrl();

            return new[]
                {
                    FilesLinkUtility.DocServiceUrl,
                    FilesLinkUtility.DocServiceUrlInternal,
                    FilesLinkUtility.DocServicePortalUrl
                };
        }

        public object GetDocServiceUrl(bool version)
        {
            var url = CommonLinkUtility.GetFullAbsolutePath(FilesLinkUtility.DocServiceApiUrl);
            if (!version)
            {
                return url;
            }

            var dsVersion = DocumentServiceConnector.GetVersion();

            return new
            {
                version = dsVersion,
                docServiceUrlApi = url,
            };
        }


        private async Task<FolderContentWrapper<T>> ToFolderContentWrapper(T folderId, Guid userIdOrGroupId, FilterType filterType, bool withSubFolders)
        {
            if (!Enum.TryParse(ApiContext.SortBy, true, out SortedByType sortBy))
            {
                sortBy = SortedByType.AZ;
            }

            var startIndex = Convert.ToInt32(ApiContext.StartIndex);
            return await FolderContentWrapperHelper.Get(await FileStorageService.GetFolderItems(folderId,
                                                                               startIndex,
                                                                               Convert.ToInt32(ApiContext.Count) - 1, //NOTE: in ApiContext +1
                                                                               filterType,
                                                                               filterType == FilterType.ByUser,
                                                                               userIdOrGroupId.ToString(),
                                                                               ApiContext.FilterValue,
                                                                               false,
                                                                               withSubFolders,
                                                                               new OrderBy(sortBy, !ApiContext.SortDescending)),
                                            startIndex);
        }

        #region wordpress

        public object GetWordpressInfo()
        {
            var token = WordpressToken.GetToken();
            if (token != null)
            {
                var meInfo = WordpressHelper.GetWordpressMeInfo(token.AccessToken);
                var blogId = JObject.Parse(meInfo).Value<string>("token_site_id");
                var wordpressUserName = JObject.Parse(meInfo).Value<string>("username");

                var blogInfo = RequestHelper.PerformRequest(WordpressLoginProvider.WordpressSites + blogId, "", "GET", "");
                var jsonBlogInfo = JObject.Parse(blogInfo);
                jsonBlogInfo.Add("username", wordpressUserName);

                blogInfo = jsonBlogInfo.ToString();
                return new
                {
                    success = true,
                    data = blogInfo
                };
            }
            return new
            {
                success = false
            };
        }

        public object DeleteWordpressInfo()
        {
            var token = WordpressToken.GetToken();
            if (token != null)
            {
                WordpressToken.DeleteToken(token);
                return new
                {
                    success = true
                };
            }
            return new
            {
                success = false
            };
        }

        public object WordpressSave(string code)
        {
            if (code == "")
            {
                return new
                {
                    success = false
                };
            }
            try
            {
                var token = OAuth20TokenHelper.GetAccessToken<WordpressLoginProvider>(ConsumerFactory, code);
                WordpressToken.SaveToken(token);
                var meInfo = WordpressHelper.GetWordpressMeInfo(token.AccessToken);
                var blogId = JObject.Parse(meInfo).Value<string>("token_site_id");

                var wordpressUserName = JObject.Parse(meInfo).Value<string>("username");

                var blogInfo = RequestHelper.PerformRequest(WordpressLoginProvider.WordpressSites + blogId, "", "GET", "");
                var jsonBlogInfo = JObject.Parse(blogInfo);
                jsonBlogInfo.Add("username", wordpressUserName);

                blogInfo = jsonBlogInfo.ToString();
                return new
                {
                    success = true,
                    data = blogInfo
                };
            }
            catch (Exception)
            {
                return new
                {
                    success = false
                };
            }
        }

        public bool CreateWordpressPost(string code, string title, string content, int status)
        {
            try
            {
                var token = WordpressToken.GetToken();
                var meInfo = WordpressHelper.GetWordpressMeInfo(token.AccessToken);
                var parser = JObject.Parse(meInfo);
                if (parser == null) return false;
                var blogId = parser.Value<string>("token_site_id");

                if (blogId != null)
                {
                    var createPost = WordpressHelper.CreateWordpressPost(title, content, status, blogId, token);
                    return createPost;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region easybib

        public object GetEasybibCitationList(int source, string data)
        {
            try
            {
                var citationList = EasyBibHelper.GetEasyBibCitationsList(source, data);
                return new
                {
                    success = true,
                    citations = citationList
                };
            }
            catch (Exception)
            {
                return new
                {
                    success = false
                };
            }

        }

        public object GetEasybibStyles()
        {
            try
            {
                var data = EasyBibHelper.GetEasyBibStyles();
                return new
                {
                    success = true,
                    styles = data
                };
            }
            catch (Exception)
            {
                return new
                {
                    success = false
                };
            }
        }

        public object EasyBibCitationBook(string citationData)
        {
            try
            {
                var citat = EasyBibHelper.GetEasyBibCitation(citationData);
                if (citat != null)
                {
                    return new
                    {
                        success = true,
                        citation = citat
                    };
                }
                else
                {
                    return new
                    {
                        success = false
                    };
                }

            }
            catch (Exception)
            {
                return new
                {
                    success = false
                };
            }
        }

        #endregion
    }

    public static class FilesControllerHelperExtention
    {
        public static DIHelper AddFilesControllerHelperService(this DIHelper services)
        {
            services.TryAddScoped<FilesControllerHelper<string>>();
            services.TryAddScoped<FilesControllerHelper<int>>();

            return services
                .AddEasyBibHelperService()
                .AddWordpressTokenService()
                .AddWordpressHelperService()
                .AddFolderContentWrapperHelperService()
                .AddFileUploaderService()
                .AddFileShareParamsService()
                .AddFileShareWrapperService()
                .AddFileOperationWraperHelperService()
                .AddFileWrapperHelperService()
                .AddFolderWrapperHelperService()
                .AddConsumerFactoryService()
                .AddDocumentServiceConnectorService()
                .AddCommonLinkUtilityService()
                .AddMessageServiceService()
                .AddThirdpartyConfigurationService()
                .AddCoreBaseSettingsService()
                .AddWebItemSecurity()
                .AddUserManagerService()
                .AddEntryManagerService()
                .AddTenantManagerService()
                .AddSecurityContextService()
                .AddDocumentServiceHelperService()
                .AddFilesLinkUtilityService()
                .AddApiContextService()
                .AddFileStorageService()
                .AddGlobalFolderHelperService()
                .AddFilesSettingsHelperService()
                .AddBoxLoginProviderService()
                .AddDropboxLoginProviderService()
                .AddOneDriveLoginProviderService()
                .AddGoogleLoginProviderService()
                .AddChunkedUploadSessionHelperService()
                .AddProductEntryPointService()
                ;
        }
    }
}
