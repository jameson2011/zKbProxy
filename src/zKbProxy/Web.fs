namespace ZkbProxy

    open System

    type HttpStatus =
        | OK
        | TooManyRequests
        | Unauthorized
        | Error
            
    type WebResponse=
        {
            Status: HttpStatus;
            Retry: TimeSpan option;
            Message: string
        }

    module Web=
        open System.Net
        open System.Net.Http

        let private userAgent = "zKbProxy (https://github.com/jameson2011/zKbProxy)"

        let httpClient()=
            let client = new System.Net.Http.HttpClient()
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent)            
            client

        let getAge (response: Net.Http.HttpResponseMessage)=
            Option.ofNullable response.Headers.Age 

        let getServerTime (response: Net.Http.HttpResponseMessage)=
            let age = getAge response
            Option.ofNullable response.Headers.Date
                |> Option.map DateTimeOffset.toUtc
                |> Option.map2 DateTime.addTimeSpan age
                    
        let getExpires (response: Net.Http.HttpResponseMessage)=
            Option.ofNullable response.Content.Headers.Expires 
                |> Option.map DateTimeOffset.toUtc

        let getWait (response: Net.Http.HttpResponseMessage) =           
            let expires = getExpires response            
            getServerTime response
                |> Option.map2 (DateTime.diff) expires 
                |> Option.map (max TimeSpan.Zero)

        let getData (client: HttpClient) (url: string) =
            async {
                try                    
                    use! resp = client.GetAsync(url) |> Async.AwaitTask
                    
                    let! result = 
                        async {                       
                            let retry = getWait resp
                            match resp.StatusCode with
                            | HttpStatusCode.OK -> 
                                    use content = resp.Content
                                    let! s = content.ReadAsStringAsync() |> Async.AwaitTask                    
                                    
                                    return { WebResponse.Status = HttpStatus.OK;
                                                Retry = retry;
                                                Message = s
                                            }
                            | x when (int x) = 429 -> 
                                    return { Status = HttpStatus.TooManyRequests;
                                                Retry = retry;
                                                Message = "";
                                            }
                            | HttpStatusCode.Unauthorized -> 
                                    return { Status = HttpStatus.Unauthorized;
                                                Retry = retry;
                                                Message = "";
                                            }
                            | x ->  return { Status = HttpStatus.Error;
                                                Retry = retry;
                                                Message = (sprintf "Error %s getting data" (x.ToString()) );
                                            }
                             }
                    return result
                with e -> 
                    return { Status = HttpStatus.Error;
                                                Retry = None;
                                                Message = (e.Message + e.StackTrace);
                                            }
            }