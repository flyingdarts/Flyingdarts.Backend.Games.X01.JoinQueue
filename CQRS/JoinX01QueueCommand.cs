using System;
using Amazon.Lambda.APIGatewayEvents;
using MediatR;

public class JoinX01QueueCommand : IRequest<APIGatewayProxyResponse>
{
    public Guid GamePlayerId { get; set; }
    public string RoomId { get; set; }
}