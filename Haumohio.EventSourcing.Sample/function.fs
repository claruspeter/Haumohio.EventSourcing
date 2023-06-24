namespace Haumohio.EventSourcing.Sample

open System.Net
open Microsoft.Azure.Functions.Worker
open Microsoft.Azure.Functions.Worker.Http
open Microsoft.Extensions.Logging

module GraphqlFunction =
  open Haumohio.Graphql 


  let run = GraphqlRunner
