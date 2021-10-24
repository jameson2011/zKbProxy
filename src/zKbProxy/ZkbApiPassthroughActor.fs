namespace ZkbProxy

open System
open System.Net.Http

type private ZkbApiPassthroughActorState = {
    lastZkbRequest: DateTime
}
with 
    static member empty = { ZkbApiPassthroughActorState.lastZkbRequest = DateTime.MinValue }

type ZkbApiPassthroughActor(config: Configuration, log: PostMessage)=
    let logSource = typeof<ZkbApiPassthroughActor>.Name
    let logException (ex: Exception) = (logSource,ex.Message) |> ActorMessage.Error |> log
    let logInfo (msg: string) = (logSource, msg) |> ActorMessage.Info |> log
    
    let logGetRequest (path: string) =              sprintf "Sending GET %s" path |> logInfo
    let logGetResp (resp: HttpResponseMessage) =    sprintf "Received %A from %A" resp.StatusCode resp.RequestMessage.RequestUri |> logInfo

    let httpClient = Web.httpClient(config)
    let zkbBaseUri =    if config.ZkbApiBaseUri.EndsWith("/") then config.ZkbApiBaseUri
                        else config.ZkbApiBaseUri + "/"

    let getResponseString(resp: HttpResponseMessage)= resp.Content.ReadAsStringAsync() |> Async.AwaitTask |> Async.RunSynchronously

    let getAsync (uri: string)  =
        async {
            
            try
                logGetRequest uri

                let! resp = httpClient.GetAsync uri |> Async.AwaitTask
            
                logGetResp resp

                let result = match resp.StatusCode with
                                | System.Net.HttpStatusCode.OK -> { WebResponse.Status = HttpStatus.OK; Retry = None; Message = (getResponseString resp) }
                                | System.Net.HttpStatusCode.NotFound -> { WebResponse.Status = HttpStatus.NotFound; Retry = None; Message = "" }
                                | System.Net.HttpStatusCode.TooManyRequests -> { WebResponse.Status = HttpStatus.TooManyRequests; Retry = None; Message = "" }
                                | System.Net.HttpStatusCode.Forbidden -> { WebResponse.Status = HttpStatus.Forbidden; Retry = None; Message = "" }
                                | _ -> { WebResponse.Status = HttpStatus.Error; Retry = None; Message = "Unknown error." }
                            
                return result
            with
            | ex -> logException ex     
                    return { WebResponse.Status = HttpStatus.Error; Retry = None; Message = ex.Message }
        }

    let getZkbApi (path: string) (lastZkbRequest: DateTime) =
        let maxIterations = 10

        let rec get (url: string) (lastZkbRequest: DateTime) iterations =
            async {
                let diff = DateTime.UtcNow - lastZkbRequest
                let pause = TimeSpan.FromSeconds(1.)
                if diff < pause then
                    sprintf "Last Zkb API request in %A: pausing for %A..." diff pause |> logInfo
                    do! Async.Sleep(pause)
                           
                let! resp = getAsync url
         
                return!
                    match resp.Status with
                    | HttpStatus.TooManyRequests when iterations <= 0 -> 
                                                    async { return { WebResponse.Status = HttpStatus.Error; Retry = None; 
                                                                        Message = maxIterations |> sprintf "Retried after %i iterations, quitting." } }
                    | HttpStatus.TooManyRequests -> 
                                                    iterations |> sprintf "Retrying, %i iteration(s) left." |> logInfo
                                                    get url DateTime.UtcNow (iterations - 1)
                    | _ ->        async { return resp }
                    
            }

        let path =  if path.StartsWith("/") then    path.Substring(1)
                    else                            path
        let uri = sprintf "%s%s" zkbBaseUri path

        get uri lastZkbRequest maxIterations


    let pipe = MessageInbox.Start(fun inbox ->
        let rec loop(state: ZkbApiPassthroughActorState) = async{           
            let! msg = inbox.Receive()

            let! newState = 
                try
                    match msg with
                    | Get (url,ch) -> 
                            async { 
                                    let! resp = getZkbApi url state.lastZkbRequest
                                    ch.Reply resp

                                    return { state with lastZkbRequest = DateTime.UtcNow}
                                }

                    | _ -> async { return state }
                with
                | ex -> logException ex
                        async { return state }

            return! loop newState
            }
        
        
        ZkbApiPassthroughActorState.empty |> loop
        )
    do pipe.Error.Add(logException)

    member this.get(path: string) = pipe.PostAndAsyncReply(fun ch -> ActorMessage.Get (path, ch) )

