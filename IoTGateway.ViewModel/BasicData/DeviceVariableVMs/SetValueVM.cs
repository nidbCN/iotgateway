﻿using Microsoft.EntityFrameworkCore;
using Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using WalkingTec.Mvvm.Core;
using IoTGateway.Model;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace IoTGateway.ViewModel.BasicData.DeviceVariableVMs
{
    public class SetValueVM : BaseVM
    {
        public List<SetValue> SetValues { get; set; } = new();
        public string 设置结果 { get; set; }

        public void Set()
        {
            try
            {
                StringValues ids, values;
                if (FC.ContainsKey("setValue.ID[]"))
                {
                    ids = (StringValues)FC["setValue.ID[]"];
                    values = (StringValues)FC["setValue.SetRawValue[]"];
                }
                else
                {
                    ids = (StringValues)FC["setValue.ID"];
                    values = (StringValues)FC["setValue.SetRawValue"];
                }

                Dictionary<string, string> kv = new(0);
                for (int i = 0; i < ids.Count; i++)
                {
                    kv[ids[i]] = values[i];
                }


                var setValues = JsonConvert.DeserializeObject<List<SetValue>>(
                    JsonConvert.SerializeObject(DC.Set<DeviceVariable>()
                        .Where(x => ids.Contains(x.ID.ToString().ToLower())).AsNoTracking()
                        .OrderBy(x => x.DeviceId).ToList()));

                var deviceService = Wtm.ServiceProvider.GetService(typeof(DeviceService)) as DeviceService;

                if (setValues != null)
                    foreach (var deviceVariables in setValues.GroupBy(x => x.DeviceId))
                    {
                        var dapThread =
                            deviceService?.DeviceThreads.FirstOrDefault(x =>
                                x.Device.ID == deviceVariables.Key);

                        if (dapThread == null) continue;

                        var deviceName = dapThread.Device.DeviceName;
                        foreach (var variable in deviceVariables)
                        {
                            var currentVariable = dapThread!.Device.DeviceVariables.FirstOrDefault(x => x.ID == variable.ID);

                            if (currentVariable == null) continue;

                            variable.DeviceName = deviceName + (!string.IsNullOrEmpty(variable.Alias)
                                ? $"->{variable.Alias}"
                                : "");
                            variable.RawValue = currentVariable.Value?.ToString();
                            variable.Value = currentVariable.CookedValue?.ToString();
                            variable.Status = currentVariable.StatusType.ToString();
                            variable.SetRawValue = kv[variable.ID.ToString()];
                        }

                        var request = new PluginInterface.RpcRequest
                        {
                            RequestId = Guid.NewGuid().ToString(),
                            DeviceName = deviceName,
                            Method = "write",
                            Params = deviceVariables.ToDictionary(x => x.Name, x => x.SetRawValue)
                        };
                        dapThread.MyMqttClient_OnExcRpc(this, request);
                    }

                设置结果 = "设置成功";
            }
            catch (Exception ex)
            {
                设置结果 = $"设置失败,{ex}";
            }
        }

        protected override void InitVM()
        {
            StringValues ids;
            if (FC.ContainsKey("setValue.ID[]"))
                ids = (StringValues)FC["setValue.ID[]"];
            else if (FC.ContainsKey("setValue.ID"))
                ids = (StringValues)FC["setValue.ID"];
            else
                ids = (StringValues)FC["Ids[]"];

            var setValues = JsonConvert.DeserializeObject<List<SetValue>>(
                JsonConvert.SerializeObject(DC.Set<DeviceVariable>()
                    .Where(x => ids.Contains(x.ID.ToString().ToLower())).AsNoTracking()
                    .OrderBy(x => x.DeviceId).ToList()));

            var deviceService = Wtm.ServiceProvider.GetService(typeof(DeviceService)) as DeviceService;

            if (setValues != null)
                foreach (var deviceVariables in setValues.GroupBy(x => x.DeviceId))
                {
                    var dapThread =
                        deviceService?.DeviceThreads.FirstOrDefault(x =>
                            x.Device.ID == deviceVariables.Key);

                    if (dapThread != null)
                    {
                        string deviceName = dapThread.Device.DeviceName;
                        foreach (var variable in deviceVariables)
                        {
                            var currentVariable = dapThread!.Device.DeviceVariables.FirstOrDefault(x => x.ID == variable.ID);
                            if (currentVariable != null)
                            {
                                variable.DeviceName = deviceName;
                                variable.RawValue = currentVariable.Value?.ToString();
                                variable.Value = currentVariable.CookedValue?.ToString();
                                variable.Status = currentVariable.StatusType.ToString();
                            }
                        }
                    }
                }

            SetValues = setValues;
            base.InitVM();
        }
    }

    public class SetValue : DeviceVariable
    {
        [Display(Name = "设备名")]
        public string DeviceName { get; set; }
        [Display(Name = "设定原值")]
        public object SetRawValue { get; set; }
        [Display(Name = "原值")]
        public string RawValue { get; set; }
        [Display(Name = "计算值")]
        public string Value { get; set; }
        [Display(Name = "状态")]
        public string Status { get; set; }
    }
}
