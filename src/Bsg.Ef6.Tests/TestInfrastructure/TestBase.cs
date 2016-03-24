﻿namespace Bsg.Ef6.Tests.TestInfrastructure
{
    using System;
    using System.Data.Entity;
    using Container;
    using Context;
    using Data.Context;
    using Extensions;
    using NUnit.Framework;

    public class TestBase
    {
        private IServiceProvider applicationContainer;

        [TestFixtureSetUp]
        public void Setup()
        {
            Database.SetInitializer(new DropCreateDatabaseAlways<PrimaryContext>());
            Database.SetInitializer(new DropCreateDatabaseAlways<SecondaryContext>());
            this.applicationContainer = new TestIocBootstrapper().BuildAutofacContainer();
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            var contextFactory = this.BuildRequestContainer().GetService<IDbContextFactory>();
            var primaryConext = contextFactory.BuildContext<PrimaryContext>();
            primaryConext.Database.Delete();

            var secondaryConext = contextFactory.BuildContext<SecondaryContext>();
            secondaryConext.Database.Delete();

            this.applicationContainer = null;
        }

        protected IServiceProvider BuildRequestContainer()
        {
            return this.applicationContainer.CreateScopedContainer();
        }

        protected TException ActionWithException<TException>(Action action) 
            where TException : Exception
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                action();
            }
            catch (TException exception)
            {
                return exception;
            }

            return null;
        }
    }
}