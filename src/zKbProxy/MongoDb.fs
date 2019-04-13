namespace ZkbProxy
    open System
    open MongoDB.Bson
    open MongoDB.Driver
    open ZkbProxy.Strings


    module Bson =
        open MongoDB.Bson.IO
        open MongoDB.Bson.Serialization


        let ofJson (json: string) =
            BsonSerializer.Deserialize<BsonDocument>(json)
        
        let toJson (bson: BsonDocument) =            
            let jsonWriterSettings = JsonWriterSettings()
            jsonWriterSettings.OutputMode <- JsonOutputMode.Strict
            jsonWriterSettings.Indent <- false
            jsonWriterSettings.IndentChars <- ""
            bson.ToJson(jsonWriterSettings)

        let ofObject (value) = 
            value 
            |> Newtonsoft.Json.JsonConvert.SerializeObject
            |> ofJson
            
        let toObject<'a> doc =
            doc |> toJson |> Newtonsoft.Json.JsonConvert.DeserializeObject<'a>

        let getDocId (bson: BsonDocument)=            
            bson.Elements 
            |> Seq.filter (fun e -> e.Name = "_id")
            |> Seq.head

        let getObjectId (bson: BsonDocument)=            
            bson
            |> getDocId
            |> (fun id -> id.Value.AsObjectId)
        
        let getId (bson: BsonDocument)=            
            bson
            |> getDocId
            |> (fun id -> id.Value.AsString)
    


    module MongoDb=
        let private defaultMongoPort = 27017

        let private idFilter id =
            sprintf @"{ _id: ""%s"" }" id
        
        let appendPort server = 
            match server |> split ":" with
            | [| name; port |] -> server
            | _ -> sprintf "%s:%i" server defaultMongoPort
            

        let resolveServers (servers: string) = 
            servers |> split ","
                    |> Seq.map appendPort
                    |> join ","

        let connectionString userName password server = 
            let servers = resolveServers server
            match userName with
            | NullOrWhitespace _ -> sprintf "mongodb://%s" servers
            | name -> sprintf "mongodb://%s:%s@%s" name password servers
        
        let setDbConnection dbName connectionString =
            match dbName with
            | NullOrWhitespace _ -> connectionString
            | x -> sprintf "%s/%s" connectionString dbName
            

        let initDb dbName (connection: string) =            
            let client= MongoClient(connection)                                            
            let db = client.GetDatabase(dbName)
            try    
                new MongoDB.Driver.BsonDocumentCommand<Object>(BsonDocument.Parse("{ping:1}"))
                        |> db.RunCommand |> ignore
                db
            with
            | :? System.TimeoutException as ex -> raise (new ApplicationException "Cannot connect to DB. Check the server name is correct, credentials & firewalls.")

        let setIndex (path: string) (collection: IMongoCollection<'a>) =                        
            let json = sprintf "{'%s': 1 }" path
            let def = IndexKeysDefinition<'a>.op_Implicit(json)
            
            let r = collection.Indexes.CreateOne(def)

            collection
        
        let getCollection colName (db: IMongoDatabase) =
            db.GetCollection(colName)                
                            
        let initCollection indexPath server dbName collectionName userName password =
            let col = server
                        |> connectionString userName password
                        |> setDbConnection dbName
                        |> initDb dbName
                        |> getCollection collectionName
            if indexPath <> "" then col |> setIndex indexPath
            else                    col



        let killsCollection server dbName collectionName userName password=
            initCollection "_v.package.killID" server dbName collectionName userName password 
            
        let sessionCollection server dbName collectionName userName password=
            initCollection "" server dbName collectionName userName password 

        let upsert (collection: IMongoCollection<BsonDocument>) (doc: BsonDocument) =
            let opts = UpdateOptions()
            opts.IsUpsert <- true
            
            let filter = doc |> Bson.getId
                             |> idFilter |> Bson.ofJson 
                             |> FilterDefinition.op_Implicit
            collection.ReplaceOne(filter, doc, opts) |> ignore 
            
        let delete (collection: IMongoCollection<BsonDocument>) id =
            
            let filter = id |> idFilter |> Bson.ofJson 
                            |> FilterDefinition.op_Implicit
            collection.DeleteOne(filter) |> ignore
            
        let query<'a> (collection: IMongoCollection<BsonDocument>) =
            collection.AsQueryable<BsonDocument>() 
                |> Seq.map Bson.toObject<'a>

                                    
            
