using System;
using Quartz;
using Quartz.Spi;

namespace WalkingTec.Mvvm.Core.Support.Quartz
{
    public class WtmJobFactory : IJobFactory
    {
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            var rv = Activator.CreateInstance(bundle.JobDetail.GetType()) as IJob;
            return rv;
        }

        public void ReturnJob(IJob job)
        {
            throw new NotImplementedException();
        }
    }
}
