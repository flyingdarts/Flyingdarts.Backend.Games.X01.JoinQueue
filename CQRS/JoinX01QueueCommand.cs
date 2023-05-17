using System;
using Amazon.Lambda.APIGatewayEvents;
using MediatR;

public class JoinX01QueueCommand : IRequest<APIGatewayProxyResponse>
{
    public Guid PlayerId { get; set; }
    public string GameId { get; set; }
}