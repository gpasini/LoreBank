using LoreBank.SharedKernel.Api.Controllers;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.SharedKernel.Test.Unit.Controllers;

// La preuve du geste CQS qui survit à la suppression du module d'exemple :
// CqsContractTest (côté Bank) prouve le bout en bout, ces tests prouvent la
// plomberie du socle.
[TestFixture]
[TestOf(typeof(ModuleController))]
public sealed class ModuleControllerTest
{
    private sealed class ProbeController(ISender sender) : ModuleController(sender)
    {
        public Task<CommandResult> Mutate(TestCommand command) => SendAsync(
            command: command,
            cancellationToken: CancellationToken.None
        );

        public Task<CreationResult> Create(TestCreationCommand command) => CreateAsync(
            command: command,
            actionName: "GetById",
            cancellationToken: CancellationToken.None
        );
    }

    [Test]
    public async Task SendAsync_ShouldReturn204WithoutBody_WhenACommandIsSent()
    {
        // Arrange

        var sender = new RecordingSender();
        var command = new TestCommand();

        // Act

        var result = await new ProbeController(sender).Mutate(command);

        // Assert

        result.Should().BeAssignableTo<NoContentResult>();
        sender.Sent.Should().ContainSingle().Which.Should().Be(command);
    }

    [Test]
    public async Task CreateAsync_ShouldReturn201PointingAtTheAction_WhenACreationCommandIsSent()
    {
        // Arrange

        var sender = new RecordingSender();

        // Act

        var result = await new ProbeController(sender).Create(new TestCreationCommand());

        // Assert

        var created = result.Should().BeAssignableTo<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be("GetById");
        created.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(sender.CreatedId);
        created.Value.Should().BeNull("une commande ne sert aucune représentation — le client suit Location");
    }
}
