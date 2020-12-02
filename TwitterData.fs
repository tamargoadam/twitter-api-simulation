module TwitterData

open Akka.FSharp
open System.Data


// Data table setup functions

let createUserTable =
    let userData = new DataTable("USERS") 
    let cols = [|
        new DataColumn("USERNAME", System.Type.GetType("System.String")); // twitter handle
        new DataColumn("ADDRESS", typedefof<Akka.Actor.IActorRef>); // user actor address
        new DataColumn("CONNECTED", System.Type.GetType("System.Boolean")) // is the user logged in
        |]
    cols.[0].Unique <- true
    userData.Columns.AddRange(cols)
    userData.PrimaryKey <- [|userData.Columns.["USERNAME"]|]
    userData


let createTweetTable = 
    let tweetData = new DataTable("TWEETS")
    let cols = [|
        new DataColumn("ID", System.Type.GetType("System.Int32")); // unique identifier
        new DataColumn("TWEET", System.Type.GetType("System.String")); // post contents
        new DataColumn("USER", System.Type.GetType("System.String")); // poster's username
        new DataColumn("RT_ID", System.Type.GetType("System.Int32")); // origional ID if rt else null
        |]
    cols.[0].ReadOnly <- true;
    cols.[0].Unique <- true;
    tweetData.Columns.AddRange(cols)
    tweetData.PrimaryKey <- [|tweetData.Columns.["ID"]|]
    tweetData


let createHashtagTable = 
    let hashtagData = new DataTable("HASHTAGS")
    let cols = [|
        new DataColumn("TAG", System.Type.GetType("System.String")); // hashtag contents
        new DataColumn("TWEET_ID", System.Type.GetType("System.Int32")); // id of post containing tag
        |]
    hashtagData.Columns.AddRange(cols)
    hashtagData


let createMentionTable = 
    let mentionData = new DataTable("MENTIONS")
    let cols = [|
        new DataColumn("MENTIONED_NAME", System.Type.GetType("System.String")); // name of mentioned user
        new DataColumn("TWEET_ID", System.Type.GetType("System.Int32")); // id of post containing mention
        |]
    mentionData.Columns.AddRange(cols)
    mentionData

let createSubscriberTable = 
    let subscriberData = new DataTable("SUBSCRIBERS")
    let cols = [|
        new DataColumn("USER", System.Type.GetType("System.String")); // username user subscribed to
        new DataColumn("SUBSCRIBER", System.Type.GetType("System.String")); // username of subscriber
        |]
    subscriberData.Columns.AddRange(cols)
    subscriberData
 

// Dataset setup function
let createTwitterDataSet = 
    let twitterData = new DataSet("TWITTER_DATA")
    twitterData.Tables.Add(createUserTable)
    twitterData.Tables.Add(createTweetTable)
    twitterData.Tables.Add(createHashtagTable)
    twitterData.Tables.Add(createMentionTable)
    twitterData.Tables.Add(createSubscriberTable)

    // Define realations b/t tables
    let rel1 = DataRelation("TWEET_USER", twitterData.Tables.["USERS"].Columns.["USERNAME"], twitterData.Tables.["TWEETS"].Columns.["USER"])
    // let rel2 = DataRelation("RT_TWEET", twitterData.Tables.["TWEETS"].Columns.["ID"], twitterData.Tables.["TWEETS"].Columns.["RT_ID"])
    let rel3 = DataRelation("TAG_TWEET", twitterData.Tables.["TWEETS"].Columns.["ID"], twitterData.Tables.["HASHTAGS"].Columns.["TWEET_ID"])
    let rel4 = DataRelation("MENTION_TWEET", twitterData.Tables.["TWEETS"].Columns.["ID"], twitterData.Tables.["MENTIONS"].Columns.["TWEET_ID"])
    let rel5 = DataRelation("MENTION_USER", twitterData.Tables.["USERS"].Columns.["USERNAME"], twitterData.Tables.["MENTIONS"].Columns.["MENTIONED_NAME"])
    let rel6 = DataRelation("SUBED_USER", twitterData.Tables.["USERS"].Columns.["USERNAME"], twitterData.Tables.["SUBSCRIBERS"].Columns.["USER"])
    let rel7 = DataRelation("SUBER_USER", twitterData.Tables.["USERS"].Columns.["USERNAME"], twitterData.Tables.["SUBSCRIBERS"].Columns.["SUBSCRIBER"])
    twitterData.Tables.["TWEETS"].ParentRelations.Add(rel1)
    // twitterData.Tables.["TWEETS"].ParentRelations.Add(rel2)
    twitterData.Tables.["HASHTAGS"].ParentRelations.Add(rel3)
    twitterData.Tables.["MENTIONS"].ParentRelations.Add(rel4)
    twitterData.Tables.["MENTIONS"].ParentRelations.Add(rel5)
    twitterData.Tables.["SUBSCRIBERS"].ParentRelations.Add(rel6)
    twitterData.Tables.["SUBSCRIBERS"].ParentRelations.Add(rel7)

    twitterData