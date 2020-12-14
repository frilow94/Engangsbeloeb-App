// <copyright file="BamboraController.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Api
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Sampension.Api.Deposits.Api.Models;
    using Sampension.Api.Deposits.Application.Bambora.Handlers;
    using Sampension.Api.Deposits.Application.Deposits.Configurations;
    using Sampension.Api.Deposits.Application.Receipts.Handlers;
    using Sampension.Api.Deposits.Infrastructure.Exceptions;

    /// <summary>
    /// Provides end-points for creating and fetching payment data via Bambora checkout.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("deposits/v2/bambora/payments")]
    public class BamboraController : ControllerBase
    {
        private readonly IDepositConfiguration depositConfiguration;
        private readonly IMediator mediator;

        /// <summary>
        /// Initializes a new instance of the <see cref="BamboraController"/> class.
        /// </summary>
        /// <param name="depositConfiguration">The deposit configuration.</param>
        /// <param name="mediator">The mediator.</param>
        public BamboraController(IDepositConfiguration depositConfiguration, IMediator mediator)
        {
            this.depositConfiguration = depositConfiguration;
            this.mediator = mediator;
        }

        /// <summary>
        /// Creates a Bambora checkout payment.
        /// </summary>
        /// <param name="user">The currently logged in user.</param>
        /// <param name="params">The create checkout payment parameters.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>200 OK with a <see cref="CreateBamboraPayment.Response"/> object.</returns>
        [HttpPost]
        [Route("")]
        public async Task<ActionResult<CreateBamboraPayment.Response>> Post(
            CurrentUser user,
            [FromBody] BamboraPaymentParams @params,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Extract arguments.
                var currency = @params.Currency;
                var sourcerType = @params.SourceType;

                // Extract and set configuration data.
                var depositLimit = this.depositConfiguration.GetDepositLimit(currency, sourcerType);

                // Create and send request to the handler.
                var request = CreateBamboraPayment.Command.Create(@params, depositLimit, user);
                var response = await this.mediator
                    .Send(request, cancellationToken)
                    .ConfigureAwait(false);

                // Return 200 OK.
                return this.Ok(response);
            }
            catch (Exception ex)
            {
                // Return handled exception action.
                if (ex is IHttpStatusException httpStatusException)
                {
                    return httpStatusException.ToAction(this);
                }

                // Re-throw unhandled exception.
                throw new DepositsException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Callback end-point called by Bambora checkout for updating checkout payments.
        /// </summary>
        /// <param name="params">The callback params sent by Bambora checkout.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>200 OK.</returns>
        [HttpGet]
        [AllowAnonymous]
        [Route("callback")]
        public async Task<ActionResult<DigestBamboraPaymentCallback.Response>> Get(
            [FromQuery] BamboraCallbackParams @params, CancellationToken cancellationToken = default)
        {
            try
            {
                // Create and send request to the Bambora payment callback handler.
                var paymentRequest = DigestBamboraPaymentCallback.Command.Create(@params);
                await this.mediator
                    .Send(paymentRequest, cancellationToken)
                    .ConfigureAwait(false);

                // Create and send request to the Bambora receipt callback handler.
                var receiptRequest = DigestBamboraReceiptCallback.Command.Create(@params);
                await this.mediator
                    .Send(receiptRequest, cancellationToken)
                    .ConfigureAwait(false);

                // Return 200 OK.
                return this.Ok();
            }
            catch (Exception ex)
            {
                // Return handled exception action.
                if (ex is IHttpStatusException httpStatusException)
                {
                    return httpStatusException.ToAction(this);
                }

                // Re-throw unhandled exception.
                throw new DepositsException(ex.Message, ex);
            }
        }
    }
}

// <copyright file="BamboraPaymentParams.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Api.Models
{
    using System;

    /// <summary>
    /// Parameter class for creating Bambora checkout payments.
    /// </summary>
    public class BamboraPaymentParams
    {
        /// <summary>
        /// Gets or sets the accept URL.
        /// </summary>
        public Uri AcceptUrl { get; set; }

        /// <summary>
        /// Gets or sets the checkout payment amount in minor units.
        /// Ex. 10000 (10000 / 100 = 100).
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Gets or sets the cancel URL.
        /// </summary>
        public Uri CancelUrl { get; set; }

        /// <summary>
        /// Gets or sets the unique coverage number of the targeted policy.
        /// </summary>
        public string CoverageNumber { get; set; }

        /// <summary>
        /// Gets or sets the checkout payment currency.
        /// Ex. "dkk", "eur" etc.
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// Gets or sets the e-mail used by the customer with this specific payment.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the phone number used by the customer with this specific payment.
        /// </summary>
        public PhoneNumber? Phone { get; set; }

        /// <summary>
        /// Gets or sets the source type from the GET sources endpoint.
        /// Ex. "card", "mobilepay" etc.
        /// </summary>
        public string SourceType { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the agreed upon terms.
        /// </summary>
        public int TermsId { get; set; }

        /// <summary>
        /// Container class for storing phone numbers.
        /// </summary>
        public class PhoneNumber
        {
            /// <summary>
            /// Gets or sets the country code part of the phone number.
            /// </summary>
            public string CountryCode { get; set; }

            /// <summary>
            /// Gets or sets the number part of the phone number.
            /// </summary>
            public string Number { get; set; }

            /// <summary>
            /// Converts the <see cref="PhoneNumber"/> object to a string representation.
            /// </summary>
            /// <returns>The phone number as a <see cref="string"/>.</returns>
            public override string ToString()
            {
                return $"{this.CountryCode ?? "+45"}{this.Number}";
            }
        }
    }
}

// <copyright file="BamboraCallbackParams.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Api.Models
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using Sampension.Api.Deposits.Api.ModelBinders;
    using Sampension.Api.Deposits.Application.Bambora.Enums;
    using Sampension.Api.Deposits.Infrastructure.Converters;
    using Sampension.Domain.Model.ValueObjects;

    /// <summary>
    /// Parameter class for Bambora checkout callbacks:
    /// don't add properties unless they're supported by the Bambora documentation - all
    /// values here are delivered via a GET call from Bambora.
    /// </summary>
    [ModelBinder(BinderType = typeof(BamboraCallbackParamsModelBinder))]
    public class BamboraCallbackParams
    {
        /// <summary>
        /// Gets or sets the payment amount in minor units.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Gets or sets the Bambora checkout callback data.
        /// </summary>
        public BamboraCheckoutCallback? Callback { get; set; }

        /// <summary>
        /// Gets or sets the masked number of the card used with the payment.
        /// </summary>
        public string? CardNumber { get; set; }

        /// <summary>
        /// Gets or sets the payment currency.
        /// </summary>
        [JsonConverter(typeof(CurrencyJsonConverter))]
        public Currency? Currency { get; set; }

        /// <summary>
        /// Gets or sets the payment date.
        /// </summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// Gets or sets the Electronic Commerce Indicator.
        /// </summary>
        public string? Eci { get; set; }

        /// <summary>
        /// Gets or sets expire month 1-12. Only present when the payment created a subscription.
        /// </summary>
        public int ExpireMonth { get; set; }

        /// <summary>
        /// Gets or sets expire year 0-99. Only present when the payment created a subscription.
        /// </summary>
        public int ExpireYear { get; set; }

        /// <summary>
        /// Gets or sets the country code of the card issuer.
        /// Only present when using paymentcard and subscription with paymentcard payments.
        /// </summary>
        public string? IssuerCountryCode { get; set; }

        /// <summary>
        /// Gets or sets the hash of all accept parameters.
        /// </summary>
        public string? Hash { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the order/payment @Sampension.
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// Gets or sets the payment type.
        /// </summary>
        public BamboraPaymentType PaymentType { get; set; }

        /// <summary>
        /// Gets or sets the reference number used by some acquirers ex. Evry.
        /// </summary>
        public string? Reference { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the subscription.
        /// Only present when the payment created a subscription.
        /// </summary>
        public int SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the time of payment completion.
        /// </summary>
        public TimeSpan? Time { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the token.
        /// Only present when the payment created a token.
        /// </summary>
        public string? TokenId { get; set; }

        /// <summary>
        /// Gets or sets the transaction fee in minor units.
        /// </summary>
        public int TransactionFee { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the transaction fee.
        /// </summary>
        public int TransactionFeeId { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the order/payment @Bambora.
        /// </summary>
        public long TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the name of the wallet type used if it is a wallet payment, e.g. MobilePay or Vipps.
        /// </summary>
        public string? WalletName { get; set; }

        /// <summary>
        /// Container class for storing Bambora checkout callback data.
        /// </summary>
        public sealed class BamboraCheckoutCallback
        {
            /// <summary>
            /// Gets or sets the ordered list of callback parameters used for generating integrity hash.
            /// </summary>
            public IEnumerable<string>? HashableParams { get; set; }

            /// <summary>
            /// Gets or sets the serialized value of the successfully bound Bambora callback parameters.
            /// </summary>
            public string? Json { get; set; }

            /// <summary>
            /// Gets or sets the callback request url.
            /// </summary>
            public Uri? RequestUrl { get; set; }
        }
    }
}

// <copyright file="BamboraCallbackParamsModelBinder.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Api.ModelBinders
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Newtonsoft.Json;
    using Sampension.Api.Deposits.Api.Models;
    using Sampension.Api.Deposits.Application.Bambora.Enums;
    using Sampension.Api.Deposits.Infrastructure.Exceptions;
    using Sampension.Domain.Model.ValueObjects;

    /// <summary>
    /// Model binder for binding callback parameters from Bambora checkout payments.
    /// </summary>
    public class BamboraCallbackParamsModelBinder : IModelBinder
    {
        /// <inheritdoc />
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            try
            {
                var request = bindingContext.HttpContext?.Request;
                var queryParams = request?.Query;
                if (queryParams?.Any() != true)
                {
                    return Task.CompletedTask;
                }

                var amount = ConvertToInteger(queryParams["amount"]);
                var cardnumber = ConvertToString(queryParams["cardno"]);
                var currency = ConvertToCurrency(queryParams["currency"]);
                var date = ConvertToDate(queryParams["date"]);
                var eci = ConvertToString(queryParams["eci"]);
                var expireMonth = ConvertToInteger(queryParams["expmonth"]);
                var expireYear = ConvertToInteger(queryParams["expyear"]);
                var issuerCountry = ConvertToString(queryParams["issuercountry"]);
                var hash = ConvertToString(queryParams["hash"]);
                var hashableParams = queryParams.Where(x => x.Key != "hash").Select(x => x.Value.ToString());
                var orderId = ConvertToInteger(queryParams["orderid"]);
                var paymentType = ConvertToEnum<BamboraPaymentType>(queryParams["paymenttype"]);
                var reference = ConvertToString(queryParams["reference"]);
                var subscriptionId = ConvertToInteger(queryParams["subscriptionid"]);
                var time = ConvertToTimeSpan(queryParams["time"]);
                var tokenId = ConvertToString(queryParams["tokenid"]);
                var transactionFee = ConvertToInteger(queryParams["txnfee"]);
                var transactionFeeId = ConvertToInteger(queryParams["feeid"]);
                var transactionId = ConvertToLong(queryParams["txnid"]);
                var walletName = ConvertToString(queryParams["walletname"]);

                var callback = new BamboraCallbackParams.BamboraCheckoutCallback
                {
                    HashableParams = hashableParams,
                    Json = null,
                    RequestUrl = new Uri($"{request.Scheme}://{request.Host}/{request.Path}{request.QueryString}"),
                };

                var bamboraCallbackParams = new BamboraCallbackParams
                {
                    Amount = amount,
                    Callback = callback,
                    CardNumber = cardnumber ?? "NA",
                    Currency = currency,
                    Date = date,
                    Eci = eci,
                    ExpireMonth = expireMonth,
                    ExpireYear = expireYear,
                    IssuerCountryCode = issuerCountry ?? "DNK",
                    Hash = hash,
                    OrderId = orderId,
                    PaymentType = paymentType,
                    Reference = reference,
                    SubscriptionId = subscriptionId,
                    Time = time,
                    TokenId = tokenId,
                    TransactionFee = transactionFee,
                    TransactionFeeId = transactionFeeId,
                    TransactionId = transactionId,
                    WalletName = walletName,
                };

                bamboraCallbackParams.Callback.Json = JsonConvert.SerializeObject(bamboraCallbackParams);

                bindingContext.Result = ModelBindingResult.Success(bamboraCallbackParams);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new DepositsException(ex.Message, ex);
            }
        }

        private static Currency? ConvertToCurrency(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var isInt = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue);
            return isInt
                ? Currency.FromNumericCode(intValue)
                : Currency.FromAlphaCode(value);
        }

        private static DateTime? ConvertToDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var year = int.Parse(value.Substring(0, 4), NumberStyles.Integer, CultureInfo.InvariantCulture);
            var month = int.Parse(value.Substring(4, 2), NumberStyles.Integer, CultureInfo.InvariantCulture);
            var day = int.Parse(value.Substring(6, 2), NumberStyles.Integer, CultureInfo.InvariantCulture);

            return new DateTime(year, month, day);
        }

        private static T ConvertToEnum<T>(string value)
        {
            value ??= "0";
            var intValue = ConvertToInteger(value);
            return (T)Enum.ToObject(typeof(T), intValue);
        }

        private static int ConvertToInteger(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0
                : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static long ConvertToLong(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? 0
                : long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static string? ConvertToString(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value;
        }

        private static TimeSpan? ConvertToTimeSpan(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var hours = int.Parse(value.Substring(0, 2), NumberStyles.Integer, CultureInfo.InvariantCulture);
            var minutes = int.Parse(value.Substring(2, 2), NumberStyles.Integer, CultureInfo.InvariantCulture);
            const int seconds = 0;

            return new TimeSpan(hours, minutes, seconds);
        }
    }
}

// <copyright file="CreateBamboraPayment.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Application.Bambora.Handlers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentValidation;
    using MediatR;
    using Sampension.Api.Deposits.Api.Models;
    using Sampension.Api.Deposits.Application.Bambora.Clients;
    using Sampension.Api.Deposits.DataAccess.Repositories.Contracts;
    using Sampension.Api.Deposits.Domain.Helpers;
    using Sampension.Api.Deposits.Domain.Models;
    using Sampension.Api.Deposits.Domain.Models.Enums;
    using Sampension.Api.Deposits.Infrastructure.Exceptions;
    using Sampension.Domain.Model.ValueObjects;

    /// <summary>
    /// Domain class for creating Bambora checkout payments.
    /// </summary>
    public static class CreateBamboraPayment
    {
        /// <summary>
        /// The request used by the handler.
        /// </summary>
        public sealed class Command : IRequest<Response>
        {
            private Command(
                Uri acceptUrl,
                int amount,
                Uri cancelUrl,
                string coverageNumber,
                string currency,
                string? email,
                string? phone,
                string sourceType,
                int termsId,
                CurrentUser user)
            {
                this.AcceptUrl = acceptUrl;
                this.Amount = amount;
                this.CancelUrl = cancelUrl;
                this.CoverageNumber = coverageNumber;
                this.Currency = currency;
                this.Email = email;
                this.Phone = phone;
                this.SourceType = sourceType;
                this.TermsId = termsId;
                this.User = user;
            }

            /// <summary>
            /// Gets the accept URL.
            /// </summary>
            public Uri AcceptUrl { get; }

            /// <summary>
            /// Gets the checkout payment amount in minor units.
            /// </summary>
            public int Amount { get; }

            /// <summary>
            /// Gets the cancel URL.
            /// </summary>
            public Uri CancelUrl { get; }

            /// <summary>
            /// Gets the unique coverage number of the targeted policy.
            /// </summary>
            public string CoverageNumber { get; }

            /// <summary>
            /// Gets the checkout payment currency.
            /// </summary>
            public string Currency { get; }

            /// <summary>
            /// Gets the customer's e-mail address for this specific payment.
            /// </summary>
            public string? Email { get; }

            /// <summary>
            /// Gets the customer's phone number for this specific payment.
            /// </summary>
            public string? Phone { get; }

            /// <summary>
            /// Gets the source type from the GET sources endpoint.
            /// </summary>
            public string SourceType { get; }

            /// <summary>
            /// Gets the unique ID of the agreed upon terms.
            /// </summary>
            public int TermsId { get; }

            /// <summary>
            /// Gets the user creating the payment.
            /// </summary>
            public CurrentUser User { get; }

            /// <summary>
            /// Create a new <see cref="Command"/> for the handler.
            /// </summary>
            /// <param name="params">The create checkout payment POST parameters.</param>
            /// <param name="depositLimit">The limits for this Bambora checkout payment.</param>
            /// <param name="user">The user creating the payment.</param>
            /// <returns>A <see cref="Command"/> object.</returns>
            public static Command Create(BamboraPaymentParams @params, DepositLimit depositLimit, CurrentUser user)
            {
                // Extract arguments.
                var acceptUrl = @params.AcceptUrl;
                var amount = @params.Amount;
                var cancelUrl = @params.CancelUrl;
                var coverageNumber = @params.CoverageNumber;
                var currency = @params.Currency ?? "DKK";
                var email = @params.Email;
                var phone = @params.Phone?.ToString();
                var sourceType = @params.SourceType;
                var termsId = @params.TermsId;

                // Create request.
                var request = new Command(
                    acceptUrl,
                    amount,
                    cancelUrl,
                    coverageNumber,
                    currency,
                    email,
                    phone,
                    sourceType,
                    termsId,
                    user);

                // Validate request.
                var validator = new RequestValidationCollection(depositLimit);
                var validationResult = validator.Validate(request);
                if (!validationResult.IsValid)
                {
                    var message = string.Join('|', validationResult.Errors.Select(x => x.ErrorMessage));
                    throw new BadRequestException(message);
                }

                // Return request.
                return request;
            }

            private class RequestValidationCollection : AbstractValidator<Command>
            {
                public RequestValidationCollection(DepositLimit depositLimit)
                {
                    this.RuleFor(x => x.AcceptUrl)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Accept URL"));

                    this.RuleFor(x => x.Amount)
                        .LessThanOrEqualTo(depositLimit.Max)
                        .WithMessage(ErrorMessageHelper.MustBeLessThanOrEqualTo(depositLimit.Max, "Amount"));

                    this.RuleFor(x => x.Amount)
                        .GreaterThanOrEqualTo(depositLimit.Min)
                        .WithMessage(ErrorMessageHelper.MustBeGreaterThanOrEqualTo(depositLimit.Min, "Amount"));

                    this.RuleFor(x => x.CancelUrl)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Cancel URL"));

                    this.RuleFor(x => x.CoverageNumber)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Coverage number"));

                    this.RuleFor(x => x.SourceType)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Source type"));

                    this.RuleFor(x => x.TermsId)
                        .GreaterThan(0)
                        .WithMessage(ErrorMessageHelper.MustBeGreaterThan(0, "Terms ID"));

                    this.RuleFor(x => x.User)
                        .NotNull()
                        .WithMessage(ErrorMessageHelper.MustNotBeNull("User"));
                }
            }
        }

        /// <summary>
        /// The response used by the handler.
        /// </summary>
        public sealed class Response
        {
            private Response(Uri bamboraCheckoutUrl)
            {
                this.BamboraCheckoutUrl = bamboraCheckoutUrl;
            }

            /// <summary>
            /// Gets the Bambora checkout URL.
            /// </summary>
            public Uri BamboraCheckoutUrl { get; }

            /// <summary>
            /// Create a <see cref="Response"/> for the handler.
            /// </summary>
            /// <param name="sessionResponse">The session response from the Bambora checkout client.</param>
            /// <returns>A <see cref="Response"/> object.</returns>
            public static Response Create(BamboraCheckoutClient.SessionResponse sessionResponse)
            {
                // Extract arguments.
                var bamboraCheckoutUrl = sessionResponse.Url;

                // Create response.
                var response = new Response(bamboraCheckoutUrl);

                // Validate response.
                var validator = new ResponseValidationCollection();
                var validationResult = validator.Validate(response);
                if (!validationResult.IsValid)
                {
                    var message = string.Join('|', validationResult.Errors.Select(x => x.ErrorMessage));
                    throw new BadRequestException(message);
                }

                // Return response.
                return response;
            }

            private class ResponseValidationCollection : AbstractValidator<Response>
            {
                public ResponseValidationCollection()
                {
                    this.RuleFor(x => x.BamboraCheckoutUrl)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Bambora checkout URL"));
                }
            }
        }

        /// <summary>
        /// The handler for creating Bambora checkout payments.
        /// </summary>
        public class Handler : IRequestHandler<Command, Response>
        {
            private readonly IBamboraCheckoutClient checkoutClient;
            private readonly IBamboraRepository bamboraRepository;
            private readonly IPaymentRepository paymentRepository;
            private readonly IPlacementRepository placementRepository;

            private readonly PaymentProvider paymentProvider = PaymentProvider.Bambora;

            /// <summary>
            /// Initializes a new instance of the <see cref="Handler"/> class.
            /// </summary>
            /// <param name="checkoutClient">The Bambora checkout client.</param>
            /// <param name="bamboraRepository">The Bambora repository.</param>
            /// <param name="paymentRepository">The payment repository.</param>
            /// <param name="placementRepository">The placement repository.</param>
            public Handler(
                IBamboraCheckoutClient checkoutClient,
                IBamboraRepository bamboraRepository,
                IPaymentRepository paymentRepository,
                IPlacementRepository placementRepository)
            {
                this.checkoutClient = checkoutClient;
                this.bamboraRepository = bamboraRepository;
                this.paymentRepository = paymentRepository;
                this.placementRepository = placementRepository;
            }

            /// <inheritdoc />
            public async Task<Response> Handle(Command request, CancellationToken cancellationToken)
            {
                // Extract arguments.
                var acceptUrl = request.AcceptUrl;
                var amount = request.Amount;
                var cancelUrl = request.CancelUrl;
                var coverageNumber = request.CoverageNumber;
                var currency = request.Currency;
                var email = request.Email ?? request.User.Email;
                var phone = request.Phone ?? request.User.Phone;
                var sourceType = request.SourceType;
                var termsId = request.TermsId;
                var user = request.User;

                // Extract user data.
                var language = user.Language;
                var policy = user.Policies
                    .FirstOrDefault(x => x.Id.Equals(coverageNumber, StringComparison.InvariantCultureIgnoreCase));

                if (policy is null)
                {
                    throw new NotFoundException($"Policy {coverageNumber}");
                }

                var accountingGroup = Enum.Parse<AccountingGroupLite>(policy.AccountingGroup.Value, true);
                var accountingGroupName = policy.AccountingGroup.Value;
                var companyName = policy.AccountingGroup.ToCompanyName();
                var cpr = user.Cpr;
                var employerNumber = policy.EmployerNumber;
                var name = user.Name;
                var referencePrefix = accountingGroup.ToReferencePrefix();
                var subjectId = user.SubjectId;

                // Get placement ID from the repository.
                var placement = await this.placementRepository
                    .GetOpenPlacementAsync(subjectId, cancellationToken)
                    .ConfigureAwait(false);

                var placementId = placement.Id;

                // Create payment in the repository.
                var paymentId = await this.paymentRepository
                    .CreatePayment(
                        accountingGroupName,
                        amount,
                        companyName,
                        coverageNumber,
                        cpr,
                        currency,
                        email,
                        employerNumber,
                        name,
                        paymentProvider,
                        phone,
                        placementId,
                        referencePrefix,
                        termsId,
                        cancellationToken)
                    .ConfigureAwait(false);

                // Send payment data to the Bambora checkout client.
                var sessionResponse = await this.checkoutClient
                    .CreateSession(
                        acceptUrl,
                        amount,
                        cancelUrl,
                        language,
                        currency,
                        paymentId,
                        sourceType,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (sessionResponse.IsSuccess)
                {
                    // Create pending Bambora payment in the repository.
                    await this.bamboraRepository
                        .CreatePending(sessionResponse.Url, paymentId, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Populate and return response.
                return Response.Create(sessionResponse);
            }
        }
    }
}

// <copyright file="DigestBamboraPaymentCallback.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Application.Bambora.Handlers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentValidation;
    using MediatR;
    using Sampension.Api.Deposits.Api.Models;
    using Sampension.Api.Deposits.Application.Bambora.Enums;
    using Sampension.Api.Deposits.Application.Bambora.Tools;
    using Sampension.Api.Deposits.DataAccess.Repositories.Contracts;
    using Sampension.Api.Deposits.Domain.Helpers;
    using Sampension.Api.Deposits.Infrastructure.Exceptions;
    using Sampension.Domain.Model.ValueObjects;

    /// <summary>
    /// Domain class for receiving payment callbacks from Bambora checkout (for payments).
    /// </summary>
    public static class DigestBamboraPaymentCallback
    {
        /// <summary>
        /// The request used by the handler.
        /// </summary>
        public sealed class Command : IRequest<Response>
        {
            private Command(
                int amount,
                BamboraCallbackParams.BamboraCheckoutCallback? callback,
                string? cardNumber,
                Currency? currency,
                DateTimeOffset? date,
                string? eci,
                int expireMonth,
                int expireYear,
                string? issuerCountryCode,
                string? hash,
                int orderId,
                BamboraPaymentType paymentType,
                string? reference,
                int subscriptionId,
                TimeSpan? time,
                string? tokenId,
                int transactionFee,
                int transactionFeeId,
                long transactionId,
                string? walletName)
            {
                this.Amount = amount;
                this.Callback = callback;
                this.CardNumber = cardNumber;
                this.CurrencyCode = currency?.AlphaCode;
                this.Date = date ?? default;
                this.Eci = eci;
                this.ExpireMonth = expireMonth;
                this.ExpireYear = expireYear;
                this.IssuerCountryCode = issuerCountryCode;
                this.Hash = hash;
                this.OrderId = orderId;
                this.PaymentType = paymentType;
                this.Reference = reference;
                this.SubscriptionId = subscriptionId;
                this.Time = time ?? default;
                this.TokenId = tokenId;
                this.TransactionFee = transactionFee;
                this.TransactionFeeId = transactionFeeId;
                this.TransactionId = transactionId;
                this.WalletName = walletName;
            }

            /// <summary>
            /// Gets the payment amount in minor units.
            /// </summary>
            public int Amount { get; }

            /// <summary>
            /// Gets the Bambora callback data.
            /// </summary>
            public BamboraCallbackParams.BamboraCheckoutCallback? Callback { get; }

            /// <summary>
            /// Gets the masked number of the card used with the payment.
            /// </summary>
            public string? CardNumber { get; }

            /// <summary>
            /// Gets the payment currency code.
            /// </summary>
            public string? CurrencyCode { get; }

            /// <summary>
            /// Gets the payment date.
            /// </summary>
            public DateTimeOffset Date { get; }

            /// <summary>
            /// Gets the Electronic Commerce Indicator.
            /// </summary>
            public string? Eci { get; }

            /// <summary>
            /// Gets expire month 1-12. Only present when the payment created a subscription.
            /// </summary>
            public int ExpireMonth { get; }

            /// <summary>
            /// Gets expire year 0-99. Only present when the payment created a subscription.
            /// </summary>
            public int ExpireYear { get; }

            /// <summary>
            /// Gets the country code of the card issuer.
            /// </summary>
            public string? IssuerCountryCode { get; }

            /// <summary>
            /// Gets the hash of all accept parameters.
            /// </summary>
            public string? Hash { get; }

            /// <summary>
            /// Gets the unique ID of the order/payment @Sampension.
            /// </summary>
            public int OrderId { get; }

            /// <summary>
            /// gets the payment type.
            /// </summary>
            public BamboraPaymentType PaymentType { get; }

            /// <summary>
            /// Gets the reference number used by some acquirers ex. Evry.
            /// </summary>
            public string? Reference { get; }

            /// <summary>
            /// Gets the unique ID of the subscription.
            /// Only present when the payment created a subscription.
            /// </summary>
            public int SubscriptionId { get; }

            /// <summary>
            /// Gets the time of payment completion.
            /// </summary>
            public TimeSpan Time { get; }

            /// <summary>
            /// Gets the unique ID of the token.
            /// Only present when the payment created a token.
            /// </summary>
            public string? TokenId { get; }

            /// <summary>
            /// Gets the transaction fee in minor units.
            /// </summary>
            public int TransactionFee { get; }

            /// <summary>
            /// Gets the unique ID of the transaction fee.
            /// </summary>
            public int TransactionFeeId { get; }

            /// <summary>
            /// Gets the unique ID of the payment @Bambora.
            /// </summary>
            public long TransactionId { get; }

            /// <summary>
            /// Gets the name of the wallet type used if it is a wallet payment, e.g. MobilePay or Vipps.
            /// </summary>
            public string? WalletName { get; }

            /// <summary>
            /// Create a new <see cref="Command"/> for the handler.
            /// </summary>
            /// <param name="params">The Bambora checkout callback GET parameters.</param>
            /// <returns>A <see cref="Command"/> object.</returns>
            public static Command Create(BamboraCallbackParams @params)
            {
                // Extract arguments.
                var amount = @params.Amount;
                var callback = @params.Callback;
                var cardNumber = @params.CardNumber;
                var currency = @params.Currency;
                var date = @params.Date;
                var eci = @params.Eci;
                var expireMonth = @params.ExpireMonth;
                var expireYear = @params.ExpireYear;
                var issuerCountryCode = @params.IssuerCountryCode;
                var hash = @params.Hash;
                var orderId = @params.OrderId;
                var paymentType = @params.PaymentType;
                var reference = @params.Reference;
                var subscriptionId = @params.SubscriptionId;
                var time = @params.Time;
                var tokenId = @params.TokenId;
                var transactionFee = @params.TransactionFee;
                var transactionFeeId = @params.TransactionFeeId;
                var transactionId = @params.TransactionId;
                var walletName = @params.WalletName;

                // Create request.
                var request = new Command(
                    amount,
                    callback,
                    cardNumber,
                    currency,
                    date,
                    eci,
                    expireMonth,
                    expireYear,
                    issuerCountryCode,
                    hash,
                    orderId,
                    paymentType,
                    reference,
                    subscriptionId,
                    time,
                    tokenId,
                    transactionFee,
                    transactionFeeId,
                    transactionId,
                    walletName);

                // Validate request.
                var validator = new RequestValidationCollection();
                var validationResult = validator.Validate(request);
                if (!validationResult.IsValid)
                {
                    var message = string.Join('|', validationResult.Errors.Select(x => x.ErrorMessage));
                    throw new BadRequestException(message);
                }

                // Return request.
                return request;
            }

            private class RequestValidationCollection : AbstractValidator<Command>
            {
                public RequestValidationCollection()
                {
                    this.RuleFor(x => x.Amount)
                        .GreaterThan(0)
                        .WithMessage(ErrorMessageHelper.MustBeGreaterThan(0, "Amount"));

                    this.RuleFor(x => x.Callback)
                        .NotNull()
                        .WithMessage(ErrorMessageHelper.MustNotBeNull("Callback"));

                    this.RuleFor(x => x.CurrencyCode)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Currency code"));

                    this.RuleFor(x => x.Date)
                        .Must(x => x != default)
                        .WithMessage(ErrorMessageHelper.MustNotBeNull("Date"));

                    this.RuleFor(x => x.Hash)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Hash (integrity)"));

                    this.RuleFor(x => x.OrderId)
                        .NotEmpty()
                        .WithMessage(ErrorMessageHelper.MustNotBeNullOrEmpty("Order ID"));

                    this.RuleFor(x => x.PaymentType)
                        .IsInEnum()
                        .WithMessage(ErrorMessageHelper.MustBeValidTypeValue("Payment type", typeof(BamboraPaymentType)));

                    this.RuleFor(x => x.PaymentType)
                        .Must(x => x != BamboraPaymentType.None)
                        .When(x => Enum.IsDefined(typeof(BamboraPaymentType), x.PaymentType))
                        .WithMessage(ErrorMessageHelper.MustNotBeEnumValueNone("Payment type", typeof(BamboraPaymentType)));

                    this.RuleFor(x => x.Time)
                        .Must(x => x != default)
                        .WithMessage(ErrorMessageHelper.MustNotBeNull("Time"));

                    this.RuleFor(x => x.TransactionFee)
                        .GreaterThanOrEqualTo(0)
                        .WithMessage(ErrorMessageHelper.MustBeGreaterThanOrEqualTo(0, "Transaction fee"));

                    this.RuleFor(x => x.TransactionId)
                        .GreaterThan(0)
                        .WithMessage(ErrorMessageHelper.MustBeGreaterThan(0, "Transaction ID"));
                }
            }
        }

        /// <summary>
        /// The response used by the handler.
        /// </summary>
        public sealed class Response
        {
            private Response(bool isSuccess, string message)
            {
                this.IsSuccess = isSuccess;
                this.Message = message;
            }

            /// <summary>
            /// Gets a value indicating whether or not the operation was a success.
            /// </summary>
            public bool IsSuccess { get; }

            /// <summary>
            /// Gets the message explaining the reason behind the 'isSuccess' property.
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// Create a <see cref="Response"/> for the handler.
            /// </summary>
            /// <param name="isSuccess">The value indicating whether or not the operation was a success.</param>
            /// <param name="message">The message explaining the reason behind the 'isSuccess' property.</param>
            /// <returns>A <see cref="Response"/> object.</returns>
            public static Response Create(bool isSuccess, string message)
            {
                // Create and return response.
                return new Response(isSuccess, message);
            }
        }

        /// <summary>
        /// The handler for receiving payment callbacks from Bambora checkout.
        /// </summary>
        public class Handler : IRequestHandler<Command, Response>
        {
            private readonly IBamboraHasher bamboraHasher;
            private readonly IBamboraRepository bamboraRepository;
            private readonly IPaymentRepository paymentRepository;
            private readonly IWorkflowRequestQueue workflowRequestQueue;

            /// <summary>
            /// Initializes a new instance of the <see cref="Handler"/> class.
            /// </summary>
            /// <param name="bamboraHasher">The Bambora hasher.</param>
            /// <param name="bamboraRepository">The Bambora repository.</param>
            /// <param name="paymentRepository">The payment repository.</param>
            /// <param name="workflowRequestQueue">The Workflow request queue.</param>
            public Handler(
                IBamboraHasher bamboraHasher,
                IBamboraRepository bamboraRepository,
                IPaymentRepository paymentRepository,
                IWorkflowRequestQueue workflowRequestQueue)
            {
                this.bamboraHasher = bamboraHasher;
                this.bamboraRepository = bamboraRepository;
                this.paymentRepository = paymentRepository;
                this.workflowRequestQueue = workflowRequestQueue;
            }

            /// <inheritdoc />
            public async Task<Response> Handle(Command request, CancellationToken cancellationToken)
            {
                // Extract arguments.
                var amount = request.Amount;
                var callback = request.Callback;
                var cardNumber = request.CardNumber;
                var currency = request.CurrencyCode;
                var date = request.Date;
                var eci = request.Eci;
                var expireMonth = request.ExpireMonth;
                var expireYear = request.ExpireYear;
                var hash = request.Hash;
                var issuerCountry = request.IssuerCountryCode;
                var paymentId = request.OrderId;
                var paymentType = request.PaymentType;
                var reference = request.Reference;
                var subscriptionId = request.SubscriptionId;
                var time = request.Time;
                var tokenId = request.TokenId;
                var transactionFee = request.TransactionFee;
                var transactionFeeId = request.TransactionFeeId;
                var transactionId = request.TransactionId;
                var walletName = request.WalletName;

                // Get the payment from the repository.
                var payment = await this.paymentRepository
                    .GetPaymentById(paymentId, cancellationToken)
                    .ConfigureAwait(false);

                // Validate amount and currency.
                if (!payment.Amount.Equals(amount) ||
                    !payment.Currency.Equals(currency, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ConflictException("Amount and/or currency validation failed.");
                }

                // Integrity check the hash recieved from Bambora.
                var accountingGroup = payment.AccountingGroupName;
                var input = callback?.HashableParams?.ToArray();
                var inputHash = this.bamboraHasher.CreateMD5Hash(accountingGroup, input);
                if (inputHash is null ||
                    !inputHash.Equals(hash, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new ConflictException("Integrity check failed.");
                }

                // Get the checkout session URL from the initial Bambora payment.
                var initial = payment.BamboraPayments.FirstOrDefault(x => x.PaymentStatus == BamboraPaymentStatus.Pending);
                if (initial is null)
                {
                    throw new NotFoundException("Pending Bambora payment.");
                }

                // Create a captured Bambora payment in the repository with the callback data (handles duplicates).
                await this.bamboraRepository
                    .CreateCaptured(
                        cardNumber,
                        initial.CheckoutSessionUrl,
                        date.Add(time),
                        eci,
                        expireMonth,
                        expireYear,
                        inputHash,
                        issuerCountry,
                        paymentId,
                        BamboraPaymentStatus.Captured,
                        reference,
                        subscriptionId,
                        tokenId,
                        transactionFee,
                        transactionFeeId,
                        transactionId,
                        paymentType,
                        walletName,
                        cancellationToken)
                    .ConfigureAwait(false);

                // Add the payment to the workflow request queue.
                var json = callback?.Json ?? string.Empty;
                await this.workflowRequestQueue
                    .Push(json, paymentId, cancellationToken)
                    .ConfigureAwait(false);

                // Populate and return response.
                return Response.Create(true, "OK.");
            }
        }
    }
}

// <copyright file="IBamboraCheckoutClient.cs" company="Sampension A/S">
// Copyright (c) Sampension A/S. All rights reserved.
// </copyright>

namespace Sampension.Api.Deposits.Application.Bambora.Clients
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A proxy client for easy Bambora checkout service access.
    /// </summary>
    public interface IBamboraCheckoutClient
    {
        /// <summary>
        /// Create a Bambora checkout session.
        /// </summary>
        /// <param name="acceptUrl">The accept URL.</param>
        /// <param name="amount">The checkout payment amount in minor units.</param>
        /// <param name="cancelUrl">The cancel URL.</param>
        /// <param name="language">The language to display in the checkout payment window.</param>
        /// <param name="currency">The currency.</param>
        /// <param name="paymentId">The unique ID of the payment (on our end).</param>
        /// <param name="sourceType">The source type from the GET sources endpoint.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
        /// <returns>A <see cref="BamboraCheckoutClient.SessionResponse"/> object.</returns>
        Task<BamboraCheckoutClient.SessionResponse> CreateSession(
            Uri acceptUrl,
            int amount,
            Uri cancelUrl,
            string language,
            string currency,
            int paymentId,
            string sourceType,
            CancellationToken cancellationToken);
    }
}