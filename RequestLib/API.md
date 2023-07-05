<a name='assembly'></a>
# RequestLib

## Contents

- [API](#T-Kafe-RequestLib-API 'Kafe.RequestLib.API')
  - [PlayerInfoUpdate](#F-Kafe-RequestLib-API-PlayerInfoUpdate 'Kafe.RequestLib.API.PlayerInfoUpdate')
  - [CancelSentRequest(request)](#M-Kafe-RequestLib-API-CancelSentRequest-Kafe-RequestLib-API-Request- 'Kafe.RequestLib.API.CancelSentRequest(Kafe.RequestLib.API.Request)')
  - [GetPendingReceivedRequests()](#M-Kafe-RequestLib-API-GetPendingReceivedRequests 'Kafe.RequestLib.API.GetPendingReceivedRequests')
  - [GetPendingSentRequests()](#M-Kafe-RequestLib-API-GetPendingSentRequests 'Kafe.RequestLib.API.GetPendingSentRequests')
  - [HasMod(playerGuid)](#M-Kafe-RequestLib-API-HasMod-System-String- 'Kafe.RequestLib.API.HasMod(System.String)')
  - [HasRequestLib(playerGuid)](#M-Kafe-RequestLib-API-HasRequestLib-System-String- 'Kafe.RequestLib.API.HasRequestLib(System.String)')
  - [RegisterMod()](#M-Kafe-RequestLib-API-RegisterMod-System-Func{Kafe-RequestLib-API-Request,Kafe-RequestLib-API-InterceptorResult}- 'Kafe.RequestLib.API.RegisterMod(System.Func{Kafe.RequestLib.API.Request,Kafe.RequestLib.API.InterceptorResult})')
  - [ResolveReceivedRequest(request,result,metadata)](#M-Kafe-RequestLib-API-ResolveReceivedRequest-Kafe-RequestLib-API-Request,Kafe-RequestLib-API-RequestResult,System-String- 'Kafe.RequestLib.API.ResolveReceivedRequest(Kafe.RequestLib.API.Request,Kafe.RequestLib.API.RequestResult,System.String)')
  - [SendRequest(request)](#M-Kafe-RequestLib-API-SendRequest-Kafe-RequestLib-API-Request- 'Kafe.RequestLib.API.SendRequest(Kafe.RequestLib.API.Request)')
- [InterceptorResult](#T-Kafe-RequestLib-API-InterceptorResult 'Kafe.RequestLib.API.InterceptorResult')
  - [GetPreventShowingRequest(resultOverride,responseMetadata)](#M-Kafe-RequestLib-API-InterceptorResult-GetPreventShowingRequest-Kafe-RequestLib-API-RequestResult,System-String- 'Kafe.RequestLib.API.InterceptorResult.GetPreventShowingRequest(Kafe.RequestLib.API.RequestResult,System.String)')
  - [GetShowRequest()](#M-Kafe-RequestLib-API-InterceptorResult-GetShowRequest 'Kafe.RequestLib.API.InterceptorResult.GetShowRequest')
- [PlayerInfo](#T-Kafe-RequestLib-API-PlayerInfo 'Kafe.RequestLib.API.PlayerInfo')
  - [Guid](#F-Kafe-RequestLib-API-PlayerInfo-Guid 'Kafe.RequestLib.API.PlayerInfo.Guid')
  - [Username](#F-Kafe-RequestLib-API-PlayerInfo-Username 'Kafe.RequestLib.API.PlayerInfo.Username')
  - [HasMod()](#M-Kafe-RequestLib-API-PlayerInfo-HasMod 'Kafe.RequestLib.API.PlayerInfo.HasMod')
  - [HasRequestLib()](#M-Kafe-RequestLib-API-PlayerInfo-HasRequestLib 'Kafe.RequestLib.API.PlayerInfo.HasRequestLib')
- [Request](#T-Kafe-RequestLib-API-Request 'Kafe.RequestLib.API.Request')
  - [#ctor(targetPlayerGuid,message,onResponse,metadata)](#M-Kafe-RequestLib-API-Request-#ctor-System-String,System-String,System-Action{Kafe-RequestLib-API-Request,Kafe-RequestLib-API-Response},System-String- 'Kafe.RequestLib.API.Request.#ctor(System.String,System.String,System.Action{Kafe.RequestLib.API.Request,Kafe.RequestLib.API.Response},System.String)')
  - [Message](#F-Kafe-RequestLib-API-Request-Message 'Kafe.RequestLib.API.Request.Message')
  - [Metadata](#F-Kafe-RequestLib-API-Request-Metadata 'Kafe.RequestLib.API.Request.Metadata')
  - [SourcePlayerGuid](#F-Kafe-RequestLib-API-Request-SourcePlayerGuid 'Kafe.RequestLib.API.Request.SourcePlayerGuid')
  - [TargetPlayerGuid](#F-Kafe-RequestLib-API-Request-TargetPlayerGuid 'Kafe.RequestLib.API.Request.TargetPlayerGuid')
- [RequestResult](#T-Kafe-RequestLib-API-RequestResult 'Kafe.RequestLib.API.RequestResult')
  - [Accepted](#F-Kafe-RequestLib-API-RequestResult-Accepted 'Kafe.RequestLib.API.RequestResult.Accepted')
  - [Declined](#F-Kafe-RequestLib-API-RequestResult-Declined 'Kafe.RequestLib.API.RequestResult.Declined')
  - [TimedOut](#F-Kafe-RequestLib-API-RequestResult-TimedOut 'Kafe.RequestLib.API.RequestResult.TimedOut')
- [Response](#T-Kafe-RequestLib-API-Response 'Kafe.RequestLib.API.Response')
  - [#ctor()](#M-Kafe-RequestLib-API-Response-#ctor-Kafe-RequestLib-API-RequestResult,System-String- 'Kafe.RequestLib.API.Response.#ctor(Kafe.RequestLib.API.RequestResult,System.String)')
  - [Metadata](#F-Kafe-RequestLib-API-Response-Metadata 'Kafe.RequestLib.API.Response.Metadata')
  - [Result](#F-Kafe-RequestLib-API-Response-Result 'Kafe.RequestLib.API.Response.Result')

<a name='T-Kafe-RequestLib-API'></a>
## API `type`

##### Namespace

Kafe.RequestLib

##### Summary

The only class you should use to Interact with RequestLib.

<a name='F-Kafe-RequestLib-API-PlayerInfoUpdate'></a>
### PlayerInfoUpdate `constants`

##### Summary

Called whenever we receive the information about the Mods of a remote player in the current Instance.
This is useful if you need to update data of a player of whether they have RequestLib or your Mod installed.

<a name='M-Kafe-RequestLib-API-CancelSentRequest-Kafe-RequestLib-API-Request-'></a>
### CancelSentRequest(request) `method`

##### Summary

Cancels a pending request you sent.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| request | [Kafe.RequestLib.API.Request](#T-Kafe-RequestLib-API-Request 'Kafe.RequestLib.API.Request') | The request reference. |

<a name='M-Kafe-RequestLib-API-GetPendingReceivedRequests'></a>
### GetPendingReceivedRequests() `method`

##### Summary

Get the currently pending received requests.
This might be useful if you want to answer to the requests via the mod, but you don't want to use an interceptor.

##### Returns

The currently pending request received from your mod from a remote player.

##### Parameters

This method has no parameters.

<a name='M-Kafe-RequestLib-API-GetPendingSentRequests'></a>
### GetPendingSentRequests() `method`

##### Summary

Retrieve the currently pending sent requests.
This might be useful if you want to cancel a request, but you didn't save it previously.

##### Returns

The currently pending requests sent by your mod.

##### Parameters

This method has no parameters.

<a name='M-Kafe-RequestLib-API-HasMod-System-String-'></a>
### HasMod(playerGuid) `method`

##### Summary

Checks whether a remote player in the current Instance has your Mod or not.

##### Returns

Whether the remote player has your mod installed or not.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| playerGuid | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | The Remote Player Guid you want to check. |

<a name='M-Kafe-RequestLib-API-HasRequestLib-System-String-'></a>
### HasRequestLib(playerGuid) `method`

##### Summary

Checks whether a remote player in the current Instance has the RequestLib or not.

##### Returns

Whether the remote player has the RequestLib installed or not.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| playerGuid | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | The Remote Player Guid you want to check. |

<a name='M-Kafe-RequestLib-API-RegisterMod-System-Func{Kafe-RequestLib-API-Request,Kafe-RequestLib-API-InterceptorResult}-'></a>
### RegisterMod() `method`

##### Summary

Registers your Mod from the RequestLib. You should run this during the initialization of your mod.

##### Parameters

This method has no parameters.

<a name='M-Kafe-RequestLib-API-ResolveReceivedRequest-Kafe-RequestLib-API-Request,Kafe-RequestLib-API-RequestResult,System-String-'></a>
### ResolveReceivedRequest(request,result,metadata) `method`

##### Summary

Resolves manually a currently pending request you received.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| request | [Kafe.RequestLib.API.Request](#T-Kafe-RequestLib-API-Request 'Kafe.RequestLib.API.Request') | The request reference, you can get it from GetPendingReceivedRequests. |
| result | [Kafe.RequestLib.API.RequestResult](#T-Kafe-RequestLib-API-RequestResult 'Kafe.RequestLib.API.RequestResult') | Which answer should be sent as the response. |
| metadata | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | Optional metadata if you want to send extra info to the requester. |

<a name='M-Kafe-RequestLib-API-SendRequest-Kafe-RequestLib-API-Request-'></a>
### SendRequest(request) `method`

##### Summary

Sends a request to a remote player in the Instance.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| request | [Kafe.RequestLib.API.Request](#T-Kafe-RequestLib-API-Request 'Kafe.RequestLib.API.Request') | Instance of the request you want to send. Use it's constructor to create one. |

<a name='T-Kafe-RequestLib-API-InterceptorResult'></a>
## InterceptorResult `type`

##### Namespace

Kafe.RequestLib.API

##### Summary

Wrapper to hold the information of the Result of an Interceptor.

<a name='M-Kafe-RequestLib-API-InterceptorResult-GetPreventShowingRequest-Kafe-RequestLib-API-RequestResult,System-String-'></a>
### GetPreventShowingRequest(resultOverride,responseMetadata) `method`

##### Summary

Generates a response for the case when you want to prevent the display of a request. This means the request
won't appear for the user to reply.

##### Returns

An InterceptorResult for you to use on your Interceptor function.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| resultOverride | [Kafe.RequestLib.API.RequestResult](#T-Kafe-RequestLib-API-RequestResult 'Kafe.RequestLib.API.RequestResult') | The result that will be sent to the request. Defaults to not sending anything (Time out). |
| responseMetadata | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | Optional metadata that can be retrieved by the request initiator. |

<a name='M-Kafe-RequestLib-API-InterceptorResult-GetShowRequest'></a>
### GetShowRequest() `method`

##### Summary

Generates a response for the case when you don't want to prevent the display of a request. This means a request
will still appear for the user to reply to.

##### Returns

An InterceptorResult for you to use on your Interceptor function.

##### Parameters

This method has no parameters.

<a name='T-Kafe-RequestLib-API-PlayerInfo'></a>
## PlayerInfo `type`

##### Namespace

Kafe.RequestLib.API

##### Summary

Player info accessible when when we get a Remote Player's info update. Happens whenever someone with the RequestLib joins the Instance.

<a name='F-Kafe-RequestLib-API-PlayerInfo-Guid'></a>
### Guid `constants`

##### Summary

The GUID of this Remote Player.

<a name='F-Kafe-RequestLib-API-PlayerInfo-Username'></a>
### Username `constants`

##### Summary

The username of this Remote Player.

<a name='M-Kafe-RequestLib-API-PlayerInfo-HasMod'></a>
### HasMod() `method`

##### Summary

Check if the Remote Player has your mod Installed.

##### Returns

Whether this Remote Player has your mod install or not.

##### Parameters

This method has no parameters.

<a name='M-Kafe-RequestLib-API-PlayerInfo-HasRequestLib'></a>
### HasRequestLib() `method`

##### Summary

Check if the Remote Player has the RequestLib installed.

##### Returns

Whether this Remote Player has the Request Library Installed or not.

##### Parameters

This method has no parameters.

<a name='T-Kafe-RequestLib-API-Request'></a>
## Request `type`

##### Namespace

Kafe.RequestLib.API

##### Summary

Represents a request of RequestLib.

<a name='M-Kafe-RequestLib-API-Request-#ctor-System-String,System-String,System-Action{Kafe-RequestLib-API-Request,Kafe-RequestLib-API-Response},System-String-'></a>
### #ctor(targetPlayerGuid,message,onResponse,metadata) `constructor`

##### Summary

Constructor of requests, should be used by the Mods to create a request.

##### Parameters

| Name | Type | Description |
| ---- | ---- | ----------- |
| targetPlayerGuid | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | Target player guid |
| message | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | The message to be displayed on the request. |
| onResponse | [System.Action{Kafe.RequestLib.API.Request,Kafe.RequestLib.API.Response}](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.Action 'System.Action{Kafe.RequestLib.API.Request,Kafe.RequestLib.API.Response}') | The Action that will be called when the player replies or the request times out |
| metadata | [System.String](http://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=EN-US&k=k:System.String 'System.String') | Optional metadata that will be available in the Interceptor. |

<a name='F-Kafe-RequestLib-API-Request-Message'></a>
### Message `constants`

##### Summary

Gets the message to be displayed on the request.

<a name='F-Kafe-RequestLib-API-Request-Metadata'></a>
### Metadata `constants`

##### Summary

Gets the optional metadata that will be available in the Interceptor.

<a name='F-Kafe-RequestLib-API-Request-SourcePlayerGuid'></a>
### SourcePlayerGuid `constants`

##### Summary

Gets the Guid of the source player from whom the request is originating.

<a name='F-Kafe-RequestLib-API-Request-TargetPlayerGuid'></a>
### TargetPlayerGuid `constants`

##### Summary

Gets the Guid of the target player to whom the request is being sent.

<a name='T-Kafe-RequestLib-API-RequestResult'></a>
## RequestResult `type`

##### Namespace

Kafe.RequestLib.API

##### Summary

The possible results of a Request.

<a name='F-Kafe-RequestLib-API-RequestResult-Accepted'></a>
### Accepted `constants`

##### Summary

The request was accepted!

<a name='F-Kafe-RequestLib-API-RequestResult-Declined'></a>
### Declined `constants`

##### Summary

The request was declined :(

<a name='F-Kafe-RequestLib-API-RequestResult-TimedOut'></a>
### TimedOut `constants`

##### Summary

The request didn't get an answer and timed out. Requests time out after 1 minute.

<a name='T-Kafe-RequestLib-API-Response'></a>
## Response `type`

##### Namespace

Kafe.RequestLib.API

##### Summary

Represents a response of RequestLib.

<a name='M-Kafe-RequestLib-API-Response-#ctor-Kafe-RequestLib-API-RequestResult,System-String-'></a>
### #ctor() `constructor`

##### Summary

Internal constructor of responses. Should not be used by Mods!

##### Parameters

This constructor has no parameters.

<a name='F-Kafe-RequestLib-API-Response-Metadata'></a>
### Metadata `constants`

##### Summary

Gets the optional metadata related to the response. This metadata might provide additional information about the response.

<a name='F-Kafe-RequestLib-API-Response-Result'></a>
### Result `constants`

##### Summary

Gets the result of the request. The result can be TimedOut, Accepted, or Declined.
