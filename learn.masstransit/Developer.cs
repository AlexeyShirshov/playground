using MassTransit;

namespace learn.masstransit;

public class Developer : MassTransitStateMachine<TheDeveloper>
{
    public Developer()
    {
        InstanceState(x => x.CurrentState,
            Performing, // 3
            Sleeping // 4
        );

        Initially(
            When(Awake)
                .ThenAsync(async (developerCtx) =>
                {
                    var developer = developerCtx.Saga;
                    Console.WriteLine($"{developer.Id} was in {developer.State} state, now start performing");
                    await developerCtx.TransitionToState(Performing);
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        await developerCtx.Publish<ITiredEvent>(new
                        {
                            developer.Id,
                        });
                    });
                })
                .Respond(messageFactory => messageFactory.Saga)
        );

        During(Performing,
            When(Tired)
                .ThenAsync(async (developerCtx) =>
                {
                    var developer = developerCtx.Saga;
                    developer.SleepCount++;

                    if (developer.SleepCount >= 2)
                    {
                        Console.WriteLine($"{developer.Id} going home");
                        await developerCtx.TransitionToState(Final);
                    }
                    else
                    {
                        Console.WriteLine($"{developer.Id} was in {developer.State} state, now is tired, so going to sleep");
                        await developerCtx.TransitionToState(Sleeping);
                    }
                }),
            When(Awake)
                .Then((developerCtx) =>
                {
                    var developer = developerCtx.Saga;
                    Console.WriteLine($"{developer.Id} already performing, don't disturb him");
                })
                .Respond(messageFactory => messageFactory.Saga)
        );

        During(Sleeping,
            When(Awake, developerCtx => developerCtx.Saga.SleepCount < 2)
                .ThenAsync(async (developerCtx) =>
                {
                    var developer = developerCtx.Saga;
                    Console.WriteLine($"{developer.Id} was in {developer.State} state, now start performing");
                    await developerCtx.TransitionToState(Performing);
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        await developerCtx.Publish<ITiredEvent>(new
                        {
                            developer.Id,
                        });
                    });
                })
                .Respond(messageFactory => messageFactory.Saga)
        );

        During(Final,
            When(Awake)
                .Then(developerCtx =>
                {
                    var developer = developerCtx.Saga;
                    Console.WriteLine($"{developer.Id} is in home");
                })
                .Respond(messageFactory => messageFactory.Saga),
            When(Tired)
                .Then(developerCtx =>
                {
                    var developer = developerCtx.Saga;
                    Console.WriteLine($"{developer.Id} is in home");
                })
                .Respond(messageFactory => messageFactory.Saga),
            When(NewDay)
                .Then(developerCtx =>
                {
                    var developer = developerCtx.Saga;
                    developer.SleepCount = 0;
                    Console.WriteLine($"{developer.Id} have a rest and ready to work");
                })
                .TransitionTo(Initial)
                .Respond(messageFactory => messageFactory.Saga),
            When(HowYDoing)
                .Respond(messageFactory => messageFactory.Saga)
        );

        DuringAny(
            When(HowYDoing)
                .Respond(messageFactory => messageFactory.Saga)
        );
        Event(() => Awake, config => config.CorrelateById(theDeveloper => theDeveloper.Message.Id));
        Event(() => Tired, config => config.CorrelateById(theDeveloper => theDeveloper.Message.Id));
        Event(() => NewDay, config => config.CorrelateById(theDeveloper => theDeveloper.Message.Id));
        Event(() => HowYDoing, config =>
        {
            config.CorrelateById(theDeveloper => theDeveloper.Message.Id);
            config.ReadOnly = true;
            config.OnMissingInstance(behavior => behavior.Execute(context =>
            {
                context.Respond(new UnknownDeveloper());
            }));
        });
    }
    /// <summary>
    /// 3
    /// </summary>
    public State? Performing { get; set; }
    /// <summary>
    /// 4
    /// </summary>
    public State? Sleeping { get; set; }
    public Event<IAwakeEvent>? Awake { get; private set; }
    public Event<ITiredEvent>? Tired { get; private set; }
    public Event<INewDayEvent>? NewDay { get; private set; }
    public Event<IHowYDoing>? HowYDoing { get; private set; }
}
public class TheDeveloper : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public Guid Id => CorrelationId;
    public int CurrentState { get; set; }
    public DeveloperState State { get => (DeveloperState)CurrentState; }
    public int SleepCount { get; set; }
    public int Version { get; set; }
}
public class UnknownDeveloper { }
public class DeveloperStatus
{
    public int State { get; set; }
    public int SleepCount { get; set; }
}
public interface IDeveloperEvent
{
    Guid Id { get; set; }
}
public interface IAwakeEvent : IDeveloperEvent { }
public interface ITiredEvent : IDeveloperEvent { }
public interface INewDayEvent : IDeveloperEvent { }
public interface IHowYDoing : IDeveloperEvent { }
public enum DeveloperState
{
    Initial = 1,
    Final = 2,
    Performing = 3,
    Sleeping = 4
}