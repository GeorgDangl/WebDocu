using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Hangfire
{
    /// <summary>
    /// This attribute configures Hangfire to only expire succeeded job after a certain duration, currently 90 days
    /// as configured in <see cref="HANGFIRE_SUCCEEDED_JOB_EXPIRATION_DAYS"/>.
    /// </summary>
    public class HangfireJobExpirationAttribute : JobFilterAttribute, IApplyStateFilter
    {
        public const int HANGFIRE_SUCCEEDED_JOB_EXPIRATION_DAYS = 90;

        public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            context.JobExpirationTimeout = TimeSpan.FromDays(HANGFIRE_SUCCEEDED_JOB_EXPIRATION_DAYS);
        }

        public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
        {
            // No implementation
        }
    }
}
