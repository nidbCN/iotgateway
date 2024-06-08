using WalkingTec.Mvvm.Core;
using IoTGateway.Model;
using Plugin;

namespace IoTGateway.ViewModel.Config.SystemConfigVMs
{
    public partial class SystemConfigVM : BaseCRUDVM<SystemConfig>
    {

        public SystemConfigVM()
        {
        }

        protected override void InitVM()
        {
        }

        public override void DoAdd()
        {           
            base.DoAdd();
        }

        public override void DoEdit(bool updateAllFields = false)
        {
            base.DoEdit(updateAllFields);
            var myMqttClient = Wtm.ServiceProvider.GetService(typeof(MyMqttClient)) as MyMqttClient;
            myMqttClient.StartClientAsync().Wait();
        }

        public override void DoDelete()
        {
            base.DoDelete();
        }
    }
}
