namespace Haumohio.EventSourcing

type UserId = string
type Logger = UserId -> string -> unit

module Common =
  open System


  let internal fail = fun (logger:Logger) (user:UserId) (exc: Exception) -> 
      exc.ToString() |> logger user
      failwith exc.Message

  let internal DUName (x:'a) =
    match Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<'a>) with
    | case, _ -> case.Name