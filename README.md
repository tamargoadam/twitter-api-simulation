# Twitter API Simulation

An implementation of a Twitter Clone and a client simulator. Actor model powered by [Akka](https://getakka.net/). HTTP/WebSocket server powered by [Suave.io](https://suave.io/).

### Scaffolds

#### `/`

  ##### `Client/`
  [Client](/Client#Client) is a simulator for active client-side users.
  Users are spawned, make requests, and process messages recieved via websocket concurrently with use of [Akka](https://getakka.net/)'s actor system.

  ##### `Server/`

  [Server](/Server#Server) is the REST API and WebSocket server that powers the backend.
