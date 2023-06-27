module Program 

open System
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Azure.Functions.Worker
open Microsoft.Extensions.Logging
open Haumohio.Azure
open Haumohio.Azure.Jwt
open Haumohio.Graphql
open Haumohio.EventSourcing.Sample

let configureServices (services : IServiceCollection) =
    services
      .AddLogging()
      .AddSingleton(typeof<IAuthenticatedFunctionAccessor>, typeof<AuthenticatedFunctionAccessor>)
      .AddAzureFuncGraphql<Query, Mutations>()
      |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder
      .AddDebug() 
      |> ignore

let configureApp (builder: IFunctionsWorkerApplicationBuilder) =
  builder
    .UseAzureFuncJwt()
    .UseMiddleware<AuthenticatedFunctionAccessorMiddleware>()
    |> ignore

let host = 
  (new HostBuilder())
    .ConfigureServices(configureServices)
    .ConfigureLogging(configureLogging)
    .ConfigureFunctionsWorkerDefaults(configureApp)
    .Build()

do
  host.Run() 
