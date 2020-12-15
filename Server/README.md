# Server

### API supports the following requests:
- Log in/out
- Tweet
- Register user
- Retweet
- Subscribe to user
- Query tweets by subscribed to, hashtag, or mentions

### Web sockets
[Suave.io](https://suave.io/) web socket connections are established upon request from client users.

[Akka](https://getakka.net/) actors are spawned for each socket in the corresponding websocket handler.
These actors listen for new tweet broadcasts from the main server actor and forward their contents to the client via the web socket.

### User interface
A simple UI for interacting with the API endpoints and estblishing web socket connections is available via the [index.html](/Server/index.html) file and served to [localhost:8080](http://localhost:8080/) upon running the server.

### Command to run
`dotnet run`
