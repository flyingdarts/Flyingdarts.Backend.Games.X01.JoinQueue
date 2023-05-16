using System;
using System.Linq;
using Amazon.Lambda.APIGatewayEvents;
using System.Threading;
using System.Threading.Tasks;
using Flyingdarts.Persistence;
using MediatR;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.DataModel;
using Flyingdarts.Lambdas.Shared;
using Flyingdarts.Shared;
using Microsoft.Extensions.Options;
using RandomGen;
using System.Text.Json;

public class JoinX01QueueCommandHandler : IRequestHandler<JoinX01QueueCommand, APIGatewayProxyResponse>
{
    private readonly IDynamoDBContext _dbContext;
    private readonly ApplicationOptions _applicationOptions;
    public JoinX01QueueCommandHandler(IDynamoDBContext dbContext, IOptions<ApplicationOptions> applicationOptions)
    {
        _dbContext = dbContext;
        _applicationOptions = applicationOptions.Value;
    }
    public async Task<APIGatewayProxyResponse> Handle(JoinX01QueueCommand request, CancellationToken cancellationToken)
    {
        var socketMessage = new SocketMessage<JoinX01QueueCommand>();
        socketMessage.Message = request;
        socketMessage.Action = "v2/games/x01/joinqueue";

        var games = await _dbContext.FromQueryAsync<Game>(X01GamesQueryConfig(), _applicationOptions.ToOperationConfig())
            .GetRemainingAsync(cancellationToken);

        if (games.Any() && games.Any(x => x.Status == GameStatus.Qualifying))
            await JoinGame(games.First(x=> x.Status == GameStatus.Qualifying), request.GamePlayerId, cancellationToken);
        else
        {
            var roomId = Gen.Random.Text.Length(7).Invoke();
            socketMessage.Message!.RoomId = roomId;
            await CreateGame(request.GamePlayerId, roomId, cancellationToken);
        }

        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(socketMessage)
        };
    }

    private async Task JoinGame(Game game, Guid playerId, CancellationToken cancellationToken)
    {
        var gamePlayer = GamePlayer.Create(game.GameId, playerId);
        var gamePlayerWrite = _dbContext.CreateBatchWrite<GamePlayer>(_applicationOptions.ToOperationConfig()); gamePlayerWrite.AddPutItem(gamePlayer);

        await gamePlayerWrite.ExecuteAsync(cancellationToken);
    }

    private async Task CreateGame(Guid playerId, string roomId, CancellationToken cancellationToken)
    {
        var game = Game.Create(2, X01GameSettings.Create(1, 3), roomId);
        var gamePlayer = GamePlayer.Create(game.GameId, playerId);

        var gameWrite = _dbContext.CreateBatchWrite<Game>(_applicationOptions.ToOperationConfig()); gameWrite.AddPutItem(game);
        var gamePlayerWrite = _dbContext.CreateBatchWrite<GamePlayer>(_applicationOptions.ToOperationConfig()); gamePlayerWrite.AddPutItem(gamePlayer);

        await gameWrite.Combine(gamePlayerWrite).ExecuteAsync(cancellationToken);
    }

    private static QueryOperationConfig X01GamesQueryConfig()
    {
        var queryFilter = new QueryFilter("PK", QueryOperator.Equal, Constants.Game);
        return new QueryOperationConfig { Filter = queryFilter };
    }
}