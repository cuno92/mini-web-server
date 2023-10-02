﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniWebServer.MiniApp;
using MiniWebServer.Mvc.Abstraction;
using MiniWebServer.Mvc.LocalAction;

namespace MiniWebServer.Mvc
{
    public class MvcMiddleware : IMiddleware
    {
        private readonly MvcOptions options;
        private readonly IServiceCollection serviceCollection;
        private readonly ILogger<MvcMiddleware> logger;
        private readonly IActionFinder actionFinder;

        public MvcMiddleware(MvcOptions options, ILoggerFactory loggerFactory, IServiceCollection serviceCollection)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.serviceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));

            logger = loggerFactory != null ? loggerFactory.CreateLogger<MvcMiddleware>() : NullLogger<MvcMiddleware>.Instance;

            if (options.ActionFinder != null)
            {
                actionFinder = options.ActionFinder;
            }
            else
            {
                actionFinder = new LocalActionFinder();
            }
        }

        public async Task InvokeAsync(IMiniAppContext context, ICallable next, CancellationToken cancellationToken = default)
        {
            try
            {
                var actionInfo = actionFinder.Find(context);
                if (actionInfo != null)
                {
                    // build new local service collection, the new collection will contain services from app's collection and some request specific services
                    var localServiceCollection = new ServiceCollection();
                    foreach (var serv in serviceCollection)
                    {
                        localServiceCollection.Add(serv);
                    }
                    localServiceCollection.AddTransient(services => context);

                    var localServiceProvider = localServiceCollection.BuildServiceProvider();

                    if (ActivatorUtilities.CreateInstance(localServiceProvider, actionInfo.ControllerType) is Controller controller)
                    {
                        // init standard properties
                        controller.ControllerContext = new ControllerContext(context);

                        if (!CallActionMethod(localServiceProvider, controller, actionInfo, context, cancellationToken))
                        {
                            logger.LogError("Error processing action {a}", actionInfo.MethodInfo);
                            context.Response.StatusCode = Abstractions.HttpResponseCodes.InternalServerError;
                        }
                    }
                    else
                    {
                        logger.LogError("Error instantiating controller {c}", actionInfo.ControllerType);
                        context.Response.StatusCode = Abstractions.HttpResponseCodes.InternalServerError;

                        return;
                    }
                }
                else
                {
                    await next.InvokeAsync(context, cancellationToken);
                }
            } catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while processing request");
                context.Response.StatusCode = Abstractions.HttpResponseCodes.InternalServerError;

                return;
            }
        }

        private bool CallActionMethod(ServiceProvider localServiceProvider, Controller controller, ActionInfo actionInfo, IMiniAppContext context, CancellationToken cancellationToken)
        {
            /* how we execute an action?
               - get action parameters
               - foreach parameter:
                  - find a service from localServiceProvider, if found, use it as an action parameter
                  - if not found, find a parameter by name from Request, if found, use it as an action parameter
                  - if not found, return an error (500 Internal Server Error) 
               - execute the action (synchronously or asynchronously)
               - if return value is not an IViewActionResult, call result.ToString() and return a ContentResult
               - otherwise, use ViewEngine to build content using the result as input, return data generated by ViewEngine               
            */



            return false;
        }
    }
}