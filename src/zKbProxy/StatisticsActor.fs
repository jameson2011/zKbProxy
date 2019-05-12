namespace ZkbProxy

    open System

    type StatisticsActor(log: PostMessage)=
        
        let logException (ex: Exception) = ActorMessage.Error (typeof<StatisticsActor>.Name,ex.Message) |> log

        let pipe = MessageInbox.Start(fun inbox ->
            let rec loop(stats: Statistics) = async{
            
                let! msg = inbox.Receive()

                let newStats = match msg with
                                | KillJson j -> 
                                    { stats with ImportedKills = stats.ImportedKills + 1L}
                                | KillProviderStats (providerName,count, lastPull) when providerName = Actors.mainProviderName -> 
                                    let ss = { stats.MainStreamStatistics with KillBufferSize = count; LastKillPull = lastPull }
                                    { stats with MainStreamStatistics = ss }
                                | KillProviderStats (providerName,count, lastPull) -> 
                                    let map = stats.SessionStreamStatistics.Add(providerName, { KillBufferSize = count; LastKillPull = lastPull })
                                    { stats with SessionStreamStatistics = map }
                                | GetStatistics ch ->
                                    stats |> ch.Reply
                                    stats
                                | SetKillCount x ->
                                    { stats with ImportedKills = x}
                                | SessionPurged x -> 
                                    let map = stats.SessionStreamStatistics |> Map.remove x
                                    { stats with SessionStreamStatistics = map }
                                | Ping ch ->                
                                    ignore 0 |> ch.Reply
                                    stats
                                | _ -> stats

                return! loop(newStats)
            }

            loop(Statistics.empty)
        )

        do pipe.Error.Add(logException)

        member __.GetStats()=
            pipe.PostAndAsyncReply ActorMessage.GetStatistics

        interface IActor with
            member __.Post(msg) = pipe.Post msg
            member __.Request(msg) = pipe.PostAndAsyncReply (fun ch -> msg)
            member __.Ping() = pipe.PostAndAsyncReply (fun ch -> Ping ch)
