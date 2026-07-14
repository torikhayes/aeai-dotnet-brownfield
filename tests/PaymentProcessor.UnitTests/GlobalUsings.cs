global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using NSubstitute;
global using eShop.PaymentProcessor;
global using eShop.PaymentProcessor.IntegrationEvents.Events;
global using eShop.PaymentProcessor.TokenLedger.Infrastructure;
global using eShop.PaymentProcessor.TokenLedger.Model;
global using eShop.PaymentProcessor.TokenLedger.Services;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
