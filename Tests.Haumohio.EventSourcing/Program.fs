module Program =

  [<assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)>]
  do()

  let [<EntryPoint>] main _ = 0