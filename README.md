# Application Load Balancer

This repository contains implementation of John Crickett's coding challenge:
[Build Your Own Load Balancer](https://codingchallenges.fyi/challenges/challenge-load-balancer)

### Developed with
- .NET 8 LTS
- ASP.NET Core 8 Web API
- C# 12.0
- Docker (Engine v 25.0.1, Desktop v 4.21.0)

### Other open-source libraries used:
- FluentValidation 
- Serilog.AspNetCore 
- Serilog.Sinks.Console
- Ulid
- Quartz.AspNetCore
- MediatR

### Components
- [Application](/Application): Application server with a `GET` endpoint, where requests will be redirected to
- [Load Balancer](/LoadBalancer): The server which does load balancing among multiple instances of `Application`

### Features
- Load balancer with Registration APIs to register/de-register applications
- Round-robin scheduling among registered applications to balance load
- Background service to periodically monitor health of registered applications and add/remove from the list of available applications
- Handling of applications going offline/online
- Handling of HTTP request to application getting timed-out
- Forwarding of `X-Forwarded-*` headers to downstream applications

### Limitations
- Only HTTP traffic
- Only API controllers (no view, only `JSON`)
- Application need to register itself to load balancer before getting requests forwarded to them
- Only HTTP `GET` requests made to load balancer with any header, path, query strings will be forwarded *as-it* is to registered/downstream applications
- Naturally, all applications should have a `GET` endpoint that can catch any path value (`**catchAll`)
- Application must have health check endpoint implemented

### Deployment

#### Configuration

##### LoadBalancer/appsettings.json

| Key | Remarks |
|+++++|+++++++++|
| LoadBalancer__RegisteredApplicationsHealthCheckInterval | Interval (in sec) after which health check calls will be made |

> [!IMPORTANT]
> Load balancer deregisteration delay will be >= LoadBalancer__RegisteredApplicationsHealthCheckInterval

##### Application/appsettings.json

| Key | Remarks |
|+++++|+++++++++|
| LoadBalancer__Uri | Load balancer URI |
| LoadBalancer__Timeout | Timeout (in sec) for requests made to load balancer |
| Application__HealthCheckPath | Application health check path |
| Application__Timeout | Timeout (in sec) for requests made to registered application(s) |

*Running load balancer*

Open terminal window and change directory to `Source/LoadBalancer` directory

Run the following command to launch the load balancer on port `1111`:
```bash
dotnet "./bin/Debug/net8.0/LoadBalancer.dll" --urls="http://+:1111"
```

Ensure load balancer is running by running the following URL: http://localhost:1111/alb-health

*Running application(s)*

Open terminal window and change directory to `Source/Application`.

Run the following command to launch the application on port `1112`:
```bash
dotnet "./bin/Debug/net8.0/Application.dll" --urls="http://+:1112"
```

Ensure application is running by running the following visiting following URL: http://localhost:1112/<Application__HealthCheckPath>

Register application to load balancer by running the visiting following URL: http://localhost:1112/register-self

After running the above command, application will get registered to load balancer as a downstream application,
from then onwards, any HTTP `GET` request made to load balancer on path `/*` will be re-directed to application.

Deregister application to load balancer by running the following URL: http://localhost:1112/deregister-self

*Multiple applications can be launched on different ports, registered to load balancer. Traffic can be distributed among them.*