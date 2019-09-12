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
using ASC.Common.Logging;
//using System.Web;
using ASC.Common.Threading.Progress;
using ASC.Core;
using ASC.Core.Tenants;
using ASC.Core.Users;
using ASC.MessagingSystem;
//using ASC.Web.CRM.Core;
using ASC.Web.Core.Users;
//using ASC.Web.Files.Services.WCFService;
//using ASC.Web.Projects.Core.Engine;
using ASC.Web.Studio.Core.Notify;
//using Autofac;
//using CrmDaoFactory = ASC.CRM.Core.Dao.DaoFactory;
using Microsoft.AspNetCore.Http;

namespace ASC.Data.Reassigns
{
    public class ReassignProgressItem : IProgressItem
    {
        private readonly HttpContext _context;
        private readonly Dictionary<string, string> _httpHeaders;

        private readonly int _tenantId;
        private readonly Guid _currentUserId;
        private readonly bool _deleteProfile;

        //private readonly IFileStorageService _docService;
        //private readonly ProjectsReassign _projectsReassign;

        public object Id { get; set; }
        public object Status { get; set; }
        public object Error { get; set; }
        public double Percentage { get; set; }
        public bool IsCompleted { get; set; }
        public Guid FromUser { get; }
        public Guid ToUser { get; }

        public MessageService MessageService { get; }
        public QueueWorkerRemove QueueWorkerRemove { get; }
        public StudioNotifyService StudioNotifyService { get; }
        public UserManager UserManager { get; }
        public UserPhotoManager UserPhotoManager { get; }
        public SecurityContext SecurityContext { get; }

        public ReassignProgressItem(
            HttpContext context,
            MessageService messageService,
            QueueWorkerReassign queueWorkerReassign,
            QueueWorkerRemove queueWorkerRemove,
            StudioNotifyService studioNotifyService,
            UserManager userManager,
            UserPhotoManager userPhotoManager,
            SecurityContext securityContext,
            int tenantId, Guid fromUserId, Guid toUserId, Guid currentUserId, bool deleteProfile)
        {
            _context = context;
            MessageService = messageService;
            QueueWorkerRemove = queueWorkerRemove;
            StudioNotifyService = studioNotifyService;
            UserManager = userManager;
            UserPhotoManager = userPhotoManager;
            SecurityContext = securityContext;
            _httpHeaders = QueueWorker.GetHttpHeaders(context.Request);

            _tenantId = tenantId;
            FromUser = fromUserId;
            ToUser = toUserId;
            _currentUserId = currentUserId;
            _deleteProfile = deleteProfile;

            //_docService = Web.Files.Classes.Global.FileStorageService;
            //_projectsReassign = new ProjectsReassign();

            Id = queueWorkerReassign.GetProgressItemId(tenantId, fromUserId);
            Status = ProgressStatus.Queued;
            Error = null;
            Percentage = 0;
            IsCompleted = false;
        }

        public void RunJob()
        {
            var logger = LogManager.GetLogger("ASC.Web");

            try
            {
                Percentage = 0;
                Status = ProgressStatus.Started;

                var tenant = CoreContext.TenantManager.SetCurrentTenant(_tenantId);
                SecurityContext.AuthenticateMe(_tenantId, _currentUserId);

                logger.InfoFormat("reassignment of data from {0} to {1}", FromUser, ToUser);

                logger.Info("reassignment of data from documents");

                Percentage = 33;
                //_docService.ReassignStorage(_fromUserId, _toUserId);

                logger.Info("reassignment of data from projects");

                Percentage = 66;
                //_projectsReassign.Reassign(_fromUserId, _toUserId);

                if (!CoreContext.Configuration.CustomMode)
                {
                    logger.Info("reassignment of data from crm");

                    Percentage = 99;
                    //using (var scope = DIHelper.Resolve(_tenantId))
                    //{
                    //    var crmDaoFactory = scope.Resolve<CrmDaoFactory>();
                    //    crmDaoFactory.ContactDao.ReassignContactsResponsible(_fromUserId, _toUserId);
                    //    crmDaoFactory.DealDao.ReassignDealsResponsible(_fromUserId, _toUserId);
                    //    crmDaoFactory.TaskDao.ReassignTasksResponsible(_fromUserId, _toUserId);
                    //    crmDaoFactory.CasesDao.ReassignCasesResponsible(_fromUserId, _toUserId);
                    //}
                }

                SendSuccessNotify();

                Percentage = 100;
                Status = ProgressStatus.Done;

                if (_deleteProfile)
                {
                    DeleteUserProfile(tenant);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                Status = ProgressStatus.Failed;
                Error = ex.Message;
                SendErrorNotify(ex.Message);
            }
            finally
            {
                logger.Info("data reassignment is complete");
                IsCompleted = true;
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        private void SendSuccessNotify()
        {
            var fromUser = UserManager.GetUsers(FromUser);
            var toUser = UserManager.GetUsers(ToUser);

            StudioNotifyService.SendMsgReassignsCompleted(_currentUserId, fromUser, toUser);

            var fromUserName = fromUser.DisplayUserName(false, UserManager);
            var toUserName = toUser.DisplayUserName(false, UserManager);

            if (_httpHeaders != null)
                MessageService.Send(_httpHeaders, MessageAction.UserDataReassigns, MessageTarget.Create(FromUser), new[] { fromUserName, toUserName });
            else
                MessageService.Send(MessageAction.UserDataReassigns, MessageTarget.Create(FromUser), fromUserName, toUserName);
        }

        private void SendErrorNotify(string errorMessage)
        {
            var fromUser = UserManager.GetUsers(FromUser);
            var toUser = UserManager.GetUsers(ToUser);

            StudioNotifyService.SendMsgReassignsFailed(_currentUserId, fromUser, toUser, errorMessage);
        }

        private void DeleteUserProfile(Tenant tenant)
        {
            var user = UserManager.GetUsers(FromUser);
            var userName = user.DisplayUserName(false, UserManager);

            UserPhotoManager.RemovePhoto(user.ID);
            UserManager.DeleteUser(user.ID);
            QueueWorkerRemove.Start(_tenantId, user, _currentUserId, false);

            if (_httpHeaders != null)
                MessageService.Send(_httpHeaders, MessageAction.UserDeleted, MessageTarget.Create(FromUser), new[] { userName });
            else
                MessageService.Send(MessageAction.UserDeleted, MessageTarget.Create(FromUser), userName);
        }
    }
}
