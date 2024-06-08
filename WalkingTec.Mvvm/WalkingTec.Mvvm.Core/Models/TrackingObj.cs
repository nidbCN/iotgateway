using System.Collections.Generic;
using Microsoft.AspNetCore.Components.Forms;

namespace WalkingTec.Mvvm.Core.Models
{
    public class TrackingObj
    {
        public object Model { get; set; }
        public List<FieldIdentifier> ChangedFields { get; set; }
    }
}
