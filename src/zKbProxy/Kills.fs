namespace ZkbRedisqProxy
    
    open MongoDB.Bson

    module Kills=
        
        let getKillId (bson: BsonDocument)=            
            bson.Elements 
                    |> Seq.tryFind (fun e -> e.Name = "package")
                    |> Option.map (fun p -> p.Value.AsBsonDocument)
                    |> Option.map (fun d -> d.Elements |> Seq.tryFind (fun e -> e.Name = "killID"))
                    |> Option.map (fun v -> v.Value.Value.AsInt32.ToString())           

        let toPackage (bson: BsonDocument)=
            bson.Values |> Seq.filter (fun v -> v :? BsonDocument)
                        |> Seq.map (fun d -> d :?> BsonDocument)
                        |> Seq.filter (fun d -> d.Elements |> Seq.exists (fun e -> e.Name = "package"))
                        |> Seq.tryHead

