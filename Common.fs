namespace Haumohio.EventSourcing

module Common =
  open System

  let internal fail = fun (logger:string -> unit) (exc: Exception) -> 
      exc.ToString() |> logger
      failwith exc.Message

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name