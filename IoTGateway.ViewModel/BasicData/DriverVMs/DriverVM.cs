using WalkingTec.Mvvm.Core;
using IoTGateway.Model;
using Plugin;

namespace IoTGateway.ViewModel.BasicData.DriverVMs
{
    public partial class DriverVM : BaseCRUDVM<Driver>
    {

        public DriverVM()
        {
        }

        protected override void InitVM()
        {
        }

        public override void DoAdd()
        {
            var driverService = Wtm.ServiceProvider
                .GetService(typeof(DriverService)) as DriverService;
            Entity.AssembleName = driverService
                .GetAssembleNameByFileName(Entity.FileName);

            if (string.IsNullOrEmpty(Entity.AssembleName))
            {
                MSD.AddModelError("", "程序集获取失败");
                return;
            }

            base.DoAdd();
        }

        public override void DoEdit(bool updateAllFields = false)
        {
            base.DoEdit(updateAllFields);
        }

        public override void DoDelete()
        {
            base.DoDelete();
        }
    }
}
