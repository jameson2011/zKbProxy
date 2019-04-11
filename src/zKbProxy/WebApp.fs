namespace ZkbRedisqProxy

open System
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful

type WebLogger(log: PostMessage)=

    let loggerName = typeof<WebLogger>.Name
    let msgSource = Actors.messageSource loggerName

    let mapLevel lvl =
        match lvl with
        | Logging.LogLevel.Warn -> ActorMessage.Warning 
        | Logging.LogLevel.Info ->  ActorMessage.Info 
        | Logging.LogLevel.Verbose
        | Logging.LogLevel.Debug -> ActorMessage.Trace
        | Logging.LogLevel.Fatal
        | Logging.LogLevel.Error -> ActorMessage.Error 

    let sendLog lvl msgFactory =
        
        use strWriter = new System.IO.StringWriter()
        let writer = Suave.Logging.TextWriterTarget([| loggerName |], lvl, strWriter) :> Suave.Logging.Logger
                        
        writer.log lvl msgFactory
       
        strWriter.ToString() 
                    |> msgSource
                    |> mapLevel lvl 
                    |> log
                
        

    interface Suave.Logging.Logger with
        member __.name = [| loggerName |]

        member __.log lvl msg = sendLog lvl msg
                                    
        member __.logWithAck lvl msg =
            async {
                do sendLog lvl msg
            }
                    


module WebApp=
                        
        
    let webConfig (logger: WebLogger) port =
        let port = match port, System.Environment.GetEnvironmentVariable("HTTP_PLATFORM_PORT") with
                        | x,_ when x <> 0us -> x
                        | _,null -> 80us
                        | _,p -> UInt16.Parse(p)
           
            
        { defaultConfig with bindings = [ HttpBinding.create HTTP System.Net.IPAddress.Any port ]; logger = logger }


    let webRoutes (log: PostMessage) (sessionProvider: SessionProviderActor) (statsProvider: StatisticsActor) = 
        let killProvider = sessionProvider.MainProvider()
        choose
            [   GET  >=> choose [
                                
                            path "/kills/"  >=> WebServices.logRouteInvoke log 
                                            >=> WebServices.getNextLiveKill ConfigurationDefaults.DefaultTtl killProvider 
                                            >=> WebServices.setNoCache >=> WebServices.jsonMimeType
                                                                
                            pathScan "/%s/kills/" (fun sessionId -> WebServices.logRouteInvoke log >=>
                                                                    (Strings.toLower sessionId 
                                                                    |> sessionProvider.GetSession 
                                                                    |> Async.RunSynchronously
                                                                    |> WebServices.getNextLiveKill ConfigurationDefaults.DefaultTtl) )
                                                    >=> WebServices.setNoCache >=> WebServices.jsonMimeType

                            path "/kills/replay/cycle/" >=> WebServices.logRouteInvoke log 
                                                        >=> WebServices.getNextReplayKill killProvider true
                                                        >=> WebServices.setNoCache >=> WebServices.jsonMimeType
                            
                            path "/kills/replay/"       >=> WebServices.logRouteInvoke log 
                                                        >=> WebServices.getNextReplayKill killProvider false
                                                        >=> WebServices.setNoCache >=> WebServices.jsonMimeType
                                

                            path "/kills/null/" >=> WebServices.logRouteInvoke log >=> OK WebServices.emptyPackage >=> WebServices.jsonMimeType
                                
                            pathScan "/kills/%s/"  (fun (id) -> WebServices.logRouteInvoke log >=> (WebServices.getKill killProvider id )) 
                                                >=> WebServices.setNoCache 
                                
                            path "/errors/random/" >=> WebServices.logRouteInvoke log >=> WebServices.getRandomError

                            path "/stats/" >=> WebServices.logRouteInvoke log 
                                            >=> (WebServices.getStats statsProvider) 
                                            >=> WebServices.textMimeType >=> WebServices.setNoCache

                            path "/favicon.ico" >=> Suave.Successful.no_content >=> WebServices.setCache 99999999
                            ];

                POST >=> choose [                                
                            path "/kills/replay/reset/" >=> WebServices.logRouteInvoke log 
                                                        >=> WebServices.resetReplayKill killProvider 
                                                        >=> WebServices.setNoCache >=> WebServices.jsonMimeType
                        ];

            ]
            