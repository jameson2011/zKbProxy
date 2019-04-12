namespace ZkbProxy

open System
open Suave
open Suave.Operators
open ZkbProxy.Strings    
    
module WebServices=

    let private errors = [| Suave.ServerErrors.BAD_GATEWAY ""; 
                            Suave.ServerErrors.GATEWAY_TIMEOUT ""; 
                            Suave.ServerErrors.INTERNAL_ERROR ""; 
                            Suave.ServerErrors.INVALID_HTTP_VERSION; 
                            Suave.ServerErrors.SERVICE_UNAVAILABLE ""; 
                            Suave.RequestErrors.BAD_REQUEST ""; 
                            Suave.RequestErrors.FORBIDDEN ""; 
                            Suave.RequestErrors.TOO_MANY_REQUESTS "" |]

    let private pickError()=
        let rng = System.Random()
        fun () ->   let i = rng.Next(0, errors.Length)
                    errors.[i]


    let emptyPackage = @"{""package"":null}"
    let jsonMimeType = Writers.setMimeType "application/json; charset=utf-8"
    let textMimeType = Writers.setMimeType "text/plain; charset=utf-8"
                
    let setExpiry (age: int ) = age |> toString |> Writers.setHeader "Expires" 
    let setPragmaNoCache = Writers.setHeader "Pragma" "no-cache"
    let setNoCacheControl = Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"

    let setCacheControl (age: int) = 
        age * 60 |> toString |> sprintf "public, max-age=%s" |> Writers.setHeader "Cache-Control" 

    let setNoCache = setNoCacheControl >=> setPragmaNoCache >=> setExpiry 0
        
    let setCache age = setCacheControl age
        
    let logRouteInvoke (log: PostMessage) (ctx: HttpContext) =
        async {
            
            let clientIp = ctx.clientIp false []
            let msg = sprintf "Received [%s] [%s] from [%s]" (ctx.request.method.ToString()) ctx.request.path (clientIp.ToString()) 

            msg |> Actors.messageSource "WebServices" |> ActorMessage.Info |> log

            return Some ctx
        }

    let getKill (provider: IKillProviderActor) id (ctx: HttpContext)=
        async {
            let! r = (provider.RequestKill id)
                
            let payload = match r with
                            | Some j -> j
                            | _ -> emptyPackage

            let response = Successful.OK payload 

            return! response ctx >>= jsonMimeType
        }
            
        
    let getNextLiveKill (ttl: int) (provider: IKillProviderActor) (ctx: HttpContext)=
        let pause = TimeSpan.FromMilliseconds(float 100)
        let rec getNext(waiting: TimeSpan)=                 
            async {
                let sw = System.Diagnostics.Stopwatch()
                sw.Start()
                let! resp = provider.RequestNextLiveKill()
                sw.Stop()
                let waiting = waiting - sw.Elapsed
                            
                let found,payload = match resp with
                                    | Some package ->   true,package
                                    | _ ->              false,emptyPackage
                    
                match found,waiting with
                | true,_ ->                             return payload
                | false,ts when ts <= TimeSpan.Zero ->  return payload
                | _ ->                                  let! _ = Async.Sleep(int pause.TotalMilliseconds)
                                                        return! getNext (waiting - pause)
            }

        async {
            let duration = TimeSpan.FromSeconds(float ttl)
            let! response = getNext duration
            return! Successful.OK response ctx
        }

    let resetReplayKill(provider: IKillProviderActor) (ctx: HttpContext)=
        async {
            let! r = provider.RequestCachedKillReset()
            let response = sprintf """{ "Success": %b } """ r
                
            return! Successful.OK response ctx
        }

    let getNextReplayKill (provider: IKillProviderActor) cycle (ctx: HttpContext)=
        async {                
            let! resp = provider.RequestCachedKill(cycle)
                
            let payload = (match resp with
                                | None -> emptyPackage
                                | Some json -> json)
            let response = Successful.OK payload
            return! response ctx 
        }


    let getRandomError (ctx: HttpContext)=
            async {
            let response = pickError()()

            return! response ctx
        }

    let getStats (statsProvider: StatisticsActor) (ctx: HttpContext)=            

        let get (stats: StreamStatistics) name =
            sprintf "[%s] buffer: %d" name stats.KillBufferSize + Environment.NewLine +
            sprintf "Last kill delivered at %s" (DateTime.formatDate stats.LastKillPull) + Environment.NewLine

        async{
            let! stats = statsProvider.GetStats()
                
            let response = sprintf "Imported kills: %d" stats.ImportedKills + Environment.NewLine +
                            get stats.MainStreamStatistics "Live"
                
            let response = stats.SessionStreamStatistics    |> Seq.map (fun kvp -> (kvp.Key, kvp.Value))
                                                            |> Seq.sortBy (fun (n,_) -> n)
                                                            |> Seq.map (fun (n,s) -> get s n)
                                                            |> Seq.fold (fun r s -> r + Environment.NewLine + s) response
                
            return! Successful.OK response ctx
        }
            
            
