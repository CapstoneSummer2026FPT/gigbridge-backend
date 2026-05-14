using System.Linq.Expressions;
using Application.Common.Interfaces;
using Hangfire;
namespace Infrastructure.Services.BackgroundJobs;
public class HangfireJobService : IBackgroundJobService {
    public string Enqueue(Expression<Action> methodCall) {
        return BackgroundJob.Enqueue(methodCall);
    }
    public string Schedule(Expression<Action> methodCall, TimeSpan delay) {
        return BackgroundJob.Schedule(methodCall, delay);
    }
}