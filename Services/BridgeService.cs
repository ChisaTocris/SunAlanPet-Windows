using SunAlanPet.Windows.Models;

namespace SunAlanPet.Windows.Services;

public sealed record PetSpoke(PetRole From, ClipDefinition Clip, int Hop, PetRole Root);

public sealed class BridgeService
{
    public event EventHandler<PetSpoke>? Spoke;
    public void Announce(PetRole from, ClipDefinition clip, int hop = 0, PetRole? root = null)
        => Spoke?.Invoke(this, new PetSpoke(from, clip, hop, root ?? from));
}
