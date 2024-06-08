﻿using System;
using System.ComponentModel.DataAnnotations;
using WalkingTec.Mvvm.Core;
using IoTGateway.Model;
using PluginInterface;
using Plugin;

namespace IoTGateway.ViewModel.BasicData.DeviceVariableVMs
{
    public partial class DeviceVariableBatchVM : BaseBatchVM<DeviceVariable, DeviceVariable_BatchEdit>
    {
        public DeviceVariableBatchVM()
        {
            ListVM = new DeviceVariableListVM();
            LinkedVM = new DeviceVariable_BatchEdit();
        }

        public override bool DoBatchDelete()
        {
            var ret = base.DoBatchDelete();
            if (ret)
            {
                var deviceService = Wtm.ServiceProvider.GetService(typeof(DeviceService)) as DeviceService;
                UpdateDevices.UpdateVaribale(DC, deviceService, FC);
            }
            return ret;
        }

        public override bool DoBatchEdit()
        {
            var ret = base.DoBatchEdit();
            if (ret)
            {
                var deviceService = Wtm.ServiceProvider.GetService(typeof(DeviceService)) as DeviceService;
                UpdateDevices.UpdateVaribale(DC, deviceService, FC);
            }
            return ret;
        }
    }

	/// <summary>
    /// Class to define batch edit fields
    /// </summary>
    public class DeviceVariable_BatchEdit : BaseVM
    {
        [Display(Name = "变量名")]
        public String Name { get; set; }
        [Display(Name = "地址")]
        public String DeviceAddress { get; set; }
        [Display(Name = "数据类型")]
        public DataTypeEnum? DataType { get; set; }
        [Display(Name = "大小端")]
        public EndianEnum? EndianType { get; set; }
        [Display(Name = "表达式")]
        public string Expressions { get; set; }
        [Display(Name = "权限")]
        public ProtectTypeEnum? ProtectType { get; set; }
        [Display(Name = "设备别名")]
        public String Alias { get; set; }

        protected override void InitVM()
        {
        }

    }

}
