#nullable enable

using System;
using FurniSpace.Application.Common.Payments;
using FurniSpace.Domain.Entities;
using FurniSpace.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurniSpace.Application.Tests.Payments;

public sealed class SePayVietQrUrlBuilderTests
{
    [Fact]
    public void Build_WithPayment_ReturnsExpectedVietQrUrl()
    {
        var builder = new SePayVietQrUrlBuilder(Options.Create(new SePayOptions
        {
            BankCode = "MB",
            BankAccountNo = "0366638256",
            BankAccountName = "TRUONG THIEN PHU TAI LOC",
            VietQrBaseUrl = "https://vietqr.app/img",
            VietQrTemplate = "compact",
            VietQrShowInfo = true,
            VietQrStoreName = "FURNISPACE"
        }));

        var payment = new Payment
        {
            PaymentCode = "FS12345678",
            Amount = 10000m
        };

        var url = builder.Build(payment);

        Assert.Contains("acc=0366638256", url, StringComparison.Ordinal);
        Assert.Contains("bank=MB", url, StringComparison.Ordinal);
        Assert.Contains("amount=10000", url, StringComparison.Ordinal);
        Assert.Contains("des=FS12345678", url, StringComparison.Ordinal);
        Assert.Contains("template=compact", url, StringComparison.Ordinal);
        Assert.Contains("showinfo=true", url, StringComparison.Ordinal);
        Assert.Contains("holder=TRUONG%20THIEN%20PHU%20TAI%20LOC", url, StringComparison.Ordinal);
        Assert.Contains("store=FURNISPACE", url, StringComparison.Ordinal);
    }
}
