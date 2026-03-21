module Program

  [<assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)>]
  [<assembly: Xunit.CaptureConsole(CaptureOut=true)>]
  do()

  // let [<EntryPoint>] main _ = 0