namespace Adeni.Domain.Tests.Retention;

using Adeni.Domain.Retention;

public sealed class BenefitBalanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static BenefitBalance Balance(int granted = 3) =>
        BenefitBalance.Grant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Haircut", granted, "owner", null, Now, out _);

    [Fact]
    public void Grant_SetsGrantedAndAppendsLedgerRow()
    {
        var balance = BenefitBalance.Grant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Haircut", 5, "owner", null, Now, out var tx);

        Assert.Equal(5, balance.Remaining);
        Assert.Equal(BenefitTransactionKind.Grant, tx.Kind);
        Assert.Equal(1, tx.Sequence);
        Assert.Equal(5, tx.RemainingAfter);
    }

    [Fact]
    public void Reserve_ReducesRemainingAndIncrementsSequence()
    {
        var balance = Balance();
        var result = balance.Reserve(2, Guid.NewGuid(), null, "customer", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, balance.Remaining);
        Assert.Equal(2, balance.Reserved);
        Assert.Equal(2, result.Value!.Sequence);
        Assert.Equal(-2, result.Value.Delta);
    }

    [Fact]
    public void Reserve_MoreThanRemaining_FailsWithoutMutation()
    {
        var balance = Balance(1);
        var result = balance.Reserve(2, Guid.NewGuid(), null, "customer", Now);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.BenefitInsufficient, result.Error!.Code);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(1, balance.Sequence);
    }

    [Fact]
    public void Release_RestoresRemaining()
    {
        var balance = Balance();
        balance.Reserve(1, Guid.NewGuid(), null, "customer", Now);
        var result = balance.Release(1, null, null, "customer", "cancelled", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, balance.Remaining);
        Assert.Equal(0, balance.Reserved);
    }

    [Fact]
    public void Release_WithoutReservation_IsConflict()
    {
        var result = Balance().Release(1, null, null, "customer", null, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.BenefitConflict, result.Error!.Code);
    }

    [Fact]
    public void Redeem_MovesReservedToUsed()
    {
        var balance = Balance();
        balance.Reserve(2, Guid.NewGuid(), null, "customer", Now);
        var result = balance.Redeem(1, null, null, "staff", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, balance.Used);
        Assert.Equal(1, balance.Reserved);
        Assert.Equal(1, balance.Remaining);
    }

    [Fact]
    public void ForfeitRemaining_LeavesReservedAlone_AndIsNullWhenEmpty()
    {
        var balance = Balance();
        balance.Reserve(1, Guid.NewGuid(), null, "customer", Now);

        var forfeit = balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, "system", "expired", Now);

        Assert.NotNull(forfeit);
        Assert.Equal(2, balance.Forfeited);
        Assert.Equal(1, balance.Reserved);
        Assert.Equal(0, balance.Remaining);
        Assert.Null(balance.ForfeitRemaining(BenefitTransactionKind.Forfeit, "system", "expired", Now));
    }

    [Fact]
    public void Invariant_UsedReservedForfeitedNeverExceedGranted()
    {
        var balance = Balance(2);
        balance.Reserve(1, Guid.NewGuid(), null, "c", Now);
        balance.Reserve(1, Guid.NewGuid(), null, "c", Now);
        balance.Redeem(1, null, null, "s", Now);
        balance.Release(1, null, null, "c", null, Now);
        balance.ForfeitRemaining(BenefitTransactionKind.Void, "owner", null, Now);

        Assert.True(balance.Used + balance.Reserved + balance.Forfeited <= balance.Granted);
        Assert.True(balance.Remaining >= 0);
    }
}
