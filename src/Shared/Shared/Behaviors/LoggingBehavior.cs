using MediatR;

namespace Shared.Behaviors
{
    public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Handling request {typeof(TRequest).Name}");

            var response = await next(cancellationToken);

            Console.WriteLine($"Handled request {typeof(TRequest).Name}");

            return response;
        }
    }
}
