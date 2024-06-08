﻿using System;
using System.Collections.Generic;
using System.Linq;
using WalkingTec.Mvvm.Core;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using IoTGateway.Model;
using Microsoft.Extensions.Primitives;

namespace IoTGateway.ViewModel.BasicData.DeviceVMs
{
    public partial class DeviceListVM : BasePagedListVM<Device_View, DeviceSearcher>
    {
        protected override List<GridAction> InitGridAction()
        {
            return new List<GridAction>
            {
                this.MakeAction("Device","Copy",Localizer["CopyDevice"],Localizer["CopyDevice"], GridActionParameterTypesEnum.SingleId,"BasicData",600).SetIconCls("layui-icon layui-icon-template-1").SetPromptMessage("你确定复制设备，包括配置参数和变量？").SetDialogTitle(Localizer["CopyDevice"]).SetHideOnToolBar(true).SetShowInRow(true).SetBindVisiableColName("copy"),
                this.MakeAction("Device","Attribute",Localizer["RequestAttribute"],Localizer["RequestAttribute"], GridActionParameterTypesEnum.SingleId,"BasicData",600).SetIconCls("layui-icon layui-icon-download-circle").SetPromptMessage("你确定请求客户端属性和共享属性吗？").SetDialogTitle(Localizer["RequestAttribute"]).SetHideOnToolBar(true).SetShowInRow(true).SetBindVisiableColName("attribute"),
                this.MakeAction("Device","CreateGroup",Localizer["CreateGroup"],Localizer["CreateGroup"], GridActionParameterTypesEnum.NoId,"BasicData",600).SetIconCls("_wtmicon _wtmicon-zuzhiqunzu").SetDialogTitle(Localizer["CreateGroup"]).SetShowInRow(false),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.Create, Localizer["CreateDevice"],"BasicData", dialogWidth: 800,name:Localizer["CreateDevice"]).SetIconCls("layui-icon layui-icon-senior"),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.Edit, Localizer["Sys.Edit"], "BasicData", dialogWidth: 800),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.Delete, Localizer["Sys.Delete"], "BasicData", dialogWidth: 800),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.Details, Localizer["Sys.Details"], "BasicData", dialogWidth: 800),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.BatchEdit, Localizer["Sys.BatchEdit"], "BasicData", dialogWidth: 800),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.BatchDelete, Localizer["Sys.BatchDelete"], "BasicData", dialogWidth: 800),
                //this.MakeStandardAction("Device", GridActionStandardTypesEnum.Import, Localizer["Sys.Import"], "BasicData", dialogWidth: 800),
                this.MakeStandardAction("Device", GridActionStandardTypesEnum.ExportExcel, Localizer["Sys.Export"], "BasicData"),
                this.MakeAction("Device","ImportExcel",Localizer["ImportExcel"],Localizer["ImportExcel"], GridActionParameterTypesEnum.NoId,"BasicData",600).SetIconCls("layui-icon layui-icon-upload-circle").SetDialogTitle(Localizer["ImportExcel"]).SetHideOnToolBar(false).SetShowInRow(false),
            };
        }


        protected override IEnumerable<IGridColumn<Device_View>> InitGridHeader()
        {
            return new List<GridColumn<Device_View>>{
                this.MakeGridHeader(x => x.DeviceName).SetWidth(150),
                this.MakeGridHeader(x => x.Index).SetWidth(60),
                //this.MakeGridHeader(x => x.Description),
                this.MakeGridHeader(x => x.DriverName_view).SetWidth(150),
                this.MakeGridHeader(x => x.AutoStart).SetWidth(80),
                this.MakeGridHeader(x => x.CgUpload).SetWidth(100),
                this.MakeGridHeader(x => x.EnforcePeriod).SetWidth(110),
                this.MakeGridHeader(x => x.CmdPeriod).SetWidth(110),
                this.MakeGridHeader(x => x.DeviceTypeEnum).SetWidth(80),
                //this.MakeGridHeader(x => x.DeviceName_view),
                this.MakeGridHeader(x=>"copy").SetHide().SetFormat((a,b)=>{
                    if(a.DeviceTypeEnum== DeviceTypeEnum.Device)
                        return "true";
                     return "false";
                }),
                this.MakeGridHeader(x=>"attribute").SetHide().SetFormat((a,b)=>{
                    if(a.DeviceTypeEnum== DeviceTypeEnum.Device)
                        return "true";
                     return "false";
                }),
                this.MakeGridHeaderAction(width: 300)
            };
        }

        public override IOrderedQueryable<Device_View> GetSearchQuery()
        {
            var data = DC.Set<Device>().AsNoTracking().Where(x => x.DeviceTypeEnum == DeviceTypeEnum.Group).OrderBy(x => x.Index).ThenBy(x => x.DeviceName).ToList();

            var dataRet = new List<Device_View>();

            int order = 0;
            foreach (var x in data)
            {
                var itemF = new Device_View
                {
                    ID = x.ID,
                    Index = x.Index,
                    DeviceName = x.DeviceName,
                    Description = x.Description,
                    DeviceTypeEnum = x.DeviceTypeEnum,
                    DriverName_view = x.Driver?.DriverName,
                    ExtraOrder = order
                };
                dataRet.Add(itemF);
                order++;


                StringValues Ids = new();
                if (FC.ContainsKey("Ids[]"))
                {
                    Ids = (StringValues)FC["Ids[]"];
                }
                var childrens = DC.Set<Device>().AsNoTracking().Where(y => y.ParentId == x.ID).Include(x => x.Driver).OrderBy(x => x.Index).ThenBy(x => x.DeviceName).ToList();
                if (Ids.Count != 0)
                    childrens = childrens.Where(x => Ids.Contains(x.ID.ToString())).ToList();

                foreach (var y in childrens)
                {
                    var itemC = new Device_View
                    {
                        ID = y.ID,
                        Index = y.Index,
                        DeviceName = "&nbsp;&nbsp;&nbsp;&nbsp;" + y.DeviceName,
                        AutoStart = y.AutoStart,
                        CgUpload = y.CgUpload,
                        EnforcePeriod = y.EnforcePeriod,
                        CmdPeriod = y.CmdPeriod,
                        Description = y.Description,
                        DeviceTypeEnum = y.DeviceTypeEnum,
                        DriverName_view = y.Driver?.DriverName,
                        DeviceName_view = itemF.DeviceName,
                        ExtraOrder = order
                    };
                    dataRet.Add(itemC);
                }
                order++;
            }

            return dataRet.AsQueryable<Device_View>().OrderBy(x => x.ExtraOrder);
        }

    }

    public class Device_View : Device
    {
        [Display(Name = "DriverName")]
        public String DriverName_view { get; set; }
        [Display(Name = "GroupName")]
        public String DeviceName_view { get; set; }
        public int ExtraOrder { get; set; }
    }
}
