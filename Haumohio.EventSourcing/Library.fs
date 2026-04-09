namespace Haumohio.EventSourcing
open System

type UserId = string

[<CLIMutable>]
type Event<'T> = {
  at: DateTime
  by: UserId
  details: 'T
}

[<AutoOpen>]
module Extensions =

  let inline unNull defaultValue value =
    match value |> box with 
    | null -> defaultValue
    | _ -> value


  type System.Collections.Generic.IDictionary<'a, 'b> with 
    member this.GetOrDefault (key: 'a) (defaultValue: 'b) =
      if this.ContainsKey(key) then 
        this.[key]
      else
        defaultValue
        
    member this.Set (key: 'a) (value: 'b) =
      this.[key] <- value
      this
