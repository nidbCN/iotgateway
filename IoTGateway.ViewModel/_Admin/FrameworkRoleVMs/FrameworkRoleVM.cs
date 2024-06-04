// WTM默认页面 Wtm buidin page
using System.Linq;
using System.Threading.Tasks;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Core.Extensions;

namespace WalkingTec.Mvvm.Mvc.Admin.ViewModels.FrameworkRoleVMs
{
    public class FrameworkRoleVM : BaseCRUDVM<FrameworkRole>
    {
        public override DuplicatedInfo<FrameworkRole> SetDuplicatedCheck()
        {
            var fieldsInfo = CreateFieldsInfo(SimpleField(x => x.RoleName));
            fieldsInfo.AddGroup(SimpleField(x => x.RoleCode));
            return fieldsInfo;
        }

        public override void DoEdit(bool updateAllFields = false)
        {
            if (FC.ContainsKey("Entity.RoleCode"))
            {
                FC.Remove("Entity.RoleCode");
            }
            base.DoEdit(updateAllFields);
        }

        public override async Task DoDeleteAsync()
        {
            await using var tran = DC.BeginTransaction();
            try
            {
                await base.DoDeleteAsync();
                var userRoles = DC
                    .Set<FrameworkUserRole>()
                    .Where(x => x.RoleCode == Entity.RoleCode);
                DC.Set<FrameworkUserRole>()
                    .RemoveRange(userRoles);
                await DC.SaveChangesAsync();
                await tran.CommitAsync();
                await Wtm.RemoveUserCache(userRoles
                    .Select(x => x.UserCode)
                    .ToArray());
            }
            catch
            {
                await tran.RollbackAsync();
            }
        }
    }
}
