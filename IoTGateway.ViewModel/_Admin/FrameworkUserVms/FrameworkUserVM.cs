// WTM默认页面 Wtm buidin page
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Core.Extensions;

namespace WalkingTec.Mvvm.Mvc.Admin.ViewModels.FrameworkUserVms
{
    public class FrameworkUserVM : BaseCRUDVM<FrameworkUser>
    {
        [JsonIgnore]
        public List<ComboSelectListItem> AllRoles { get; set; }

        [JsonIgnore]
        public List<ComboSelectListItem> AllGroups { get; set; }
        [Display(Name = "_Admin.Role")]
        public List<string> SelectedRolesCodes { get; set; }
        [Display(Name = "_Admin.Group")]
        public List<string> SelectedGroupCodes { get; set; }


        public FrameworkUserVM()
        {
        }

        public override DuplicatedInfo<FrameworkUser> SetDuplicatedCheck()
        {
            var rv = CreateFieldsInfo(SimpleField(x => x.ITCode));
            return rv;
        }

        protected override void InitVM()
        {
            AllRoles = DC.Set<FrameworkRole>()
                .GetSelectListItems(Wtm, y => y.RoleName, y => y.RoleCode);
            AllGroups = DC.Set<FrameworkGroup>()
                .GetSelectListItems(Wtm, y => y.GroupName, y => y.GroupCode);
            SelectedRolesCodes = DC.Set<FrameworkUserRole>()
                .Where(x => x.UserCode == Entity.ITCode)
                .Select(x => x.RoleCode).ToList();
            SelectedGroupCodes = DC.Set<FrameworkUserGroup>()
                .Where(x => x.UserCode == Entity.ITCode)
                .Select(x => x.GroupCode).ToList();
        }

        protected override void ReInitVM()
        {
            AllRoles = DC.Set<FrameworkRole>()
                .GetSelectListItems(Wtm, y => y.RoleName, y => y.RoleCode);
            AllGroups = DC.Set<FrameworkGroup>()
                .GetSelectListItems(Wtm, y => y.GroupName, y => y.GroupCode);
        }

        public override async Task DoAddAsync()
        {
            await using var trans = DC.BeginTransaction();
            if (SelectedRolesCodes != null)
            {
                foreach (var r in SelectedRolesCodes
                             .Select(code => new FrameworkUserRole
                             {
                                 RoleCode = code,
                                 UserCode = Entity.ITCode
                             }))
                {
                    DC.AddEntity(r);
                }
            }
            if (SelectedGroupCodes != null)
            {
                foreach (var g in SelectedGroupCodes
                             .Select(code => new FrameworkUserGroup
                         {
                             GroupCode = code,
                             UserCode = Entity.ITCode
                         }))
                {
                    DC.AddEntity(g);
                }
            }
            Entity.IsValid = true;
            Entity.Password = Utils.GetMD5String(Entity.Password);
            await base.DoAddAsync();
            if (MSD.IsValid)
            {
                await trans.CommitAsync();
            }
            else
            {
                await trans.RollbackAsync();
            }
        }

        public override async Task DoEditAsync(bool updateAllFields = false)
        {
            if (FC.ContainsKey("Entity.ITCode"))
            {
                FC.Remove("Entity.ITCode");
            }

            await using var trans = DC.BeginTransaction();
            if (SelectedRolesCodes != null)
            {
                var deleteItems = new List<Guid>();
                deleteItems.AddRange(DC.Set<FrameworkUserRole>()
                    .AsNoTracking()
                    .Where(x => x.UserCode == Entity.ITCode)
                    .Select(x => x.ID));
                foreach (var item in deleteItems)
                {
                    DC.DeleteEntity(new FrameworkUserRole { ID = item });
                }
            }
            if (SelectedGroupCodes != null)
            {
                var deleteItems = new List<Guid>();
                deleteItems.AddRange(DC.Set<FrameworkUserGroup>()
                    .AsNoTracking()
                    .Where(x => x.UserCode == Entity.ITCode)
                    .Select(x => x.ID));
                foreach (var item in deleteItems)
                {
                    DC.DeleteEntity(new FrameworkUserGroup { ID = item });
                }
            }
            if (SelectedRolesCodes != null)
            {
                foreach (var r in SelectedRolesCodes
                             .Select(code => new FrameworkUserRole
                         {
                             RoleCode = code,
                             UserCode = Entity.ITCode
                         }))
                {
                    DC.AddEntity(r);
                }
            }
            if (SelectedGroupCodes != null)
            {
                foreach (var g in SelectedGroupCodes
                             .Select(code => new FrameworkUserGroup
                         {
                             GroupCode = code,
                             UserCode = Entity.ITCode
                         }))
                {
                    DC.AddEntity(g);
                }
            }
            await base.DoEditAsync(updateAllFields);
            if (MSD.IsValid)
            {
                await trans.CommitAsync();
                await Wtm.RemoveUserCache(Entity.ITCode);
            }
            else
            {
                await trans.RollbackAsync();
            }
        }

        public override async Task DoDeleteAsync()
        {
            await using (var tran = DC.BeginTransaction())
            {
                try
                {
                    await base.DoDeleteAsync();
                    var users = DC.Set<FrameworkUserRole>()
                        .Where(x => x.UserCode == Entity.ITCode);
                    DC.Set<FrameworkUserRole>().RemoveRange(users);
                    var userGroups = DC.Set<FrameworkUserGroup>()
                        .Where(x => x.UserCode == Entity.ITCode);
                    DC.Set<FrameworkUserGroup>().RemoveRange(userGroups);
                    await DC.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                catch
                {
                    await tran.RollbackAsync();
                }
            }
            await Wtm.RemoveUserCache(Entity.ITCode.ToString());
        }

        public void ChangePassword()
        {
            Entity.Password = Utils.GetMD5String(Entity.Password);
            DC.UpdateProperty(Entity, x => x.Password);
            DC.SaveChanges();
        }
    }
}
