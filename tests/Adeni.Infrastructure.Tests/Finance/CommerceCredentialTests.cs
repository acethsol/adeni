namespace Adeni.Infrastructure.Tests.Finance;
using Adeni.Application.Finance;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using static CommerceTestSupport;
public sealed class CommerceCredentialTests
{
    private sealed class Sink:ILogEventSink
    {
        public string Message="";
        public void Emit(LogEvent logEvent)=>Message=logEvent.RenderMessage();
    }
    [Fact]
    public void Credentials_ArePurposeSeparatedVersionedAndHaveNoProductionFallback()
    {
        var tenant=Guid.NewGuid();var id=Guid.NewGuid();var nonce=CommerceCredentials.NewNonce();
        var options=new CommerceOptions{CredentialKeys=new(){["v1"]=Convert.ToBase64String(new byte[32])}};
        var credentials=new CommerceCredentials(Options.Create(options),new Host("Production"));
        var gift=credentials.Derive("gift",tenant,id,nonce,"v1");
        Assert.Equal(64,gift.Length);Assert.NotEqual(gift,credentials.Derive("link",tenant,id,nonce,"v1"));
        options.CredentialKeys["v2"]=Convert.ToBase64String(Enumerable.Repeat((byte)1,32).ToArray());options.ActiveKeyVersion="v2";
        Assert.Equal(gift,credentials.Derive("gift",tenant,id,nonce,"v1"));
        Assert.Throws<InvalidOperationException>(()=>credentials.Derive("gift",tenant,id,nonce,"missing"));
    }
    [Fact]
    public void DestructuredCredentialObjectsAndErrorRepresentations_DoNotRevealSecrets()
    {
        var sink=new Sink();using var logger=new LoggerConfiguration().Destructure.With<PiiDestructuringPolicy>().WriteTo.Sink(sink).CreateLogger();
        const string sentinel="sensitive-credential-sentinel";
        logger.Information("{Gift} {@GiftBody} {@Attempt}",new GiftCodeView(sentinel),new RedeemCommerceRequest(sentinel),new CommercePaymentAttempt{CheckoutUrl=sentinel});
        Assert.DoesNotContain(sentinel,sink.Message);
        Assert.DoesNotContain(sentinel,new CommerceRequestView(Guid.NewGuid(),"CustomCharge",10,"NGN","Charge",false,"Active",Now,Now.AddDays(7),null,sentinel).ToString());
        Assert.DoesNotContain(sentinel,new CommerceCheckoutView("pending",sentinel).ToString());
    }
}
