using System.Globalization;
using System.Runtime.ExceptionServices;
using GenSW.Application.Animals;
using GenSW.Domain.Animals;
using GenSW.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GenSW.Infrastructure.Animals;

public sealed class PostgreSqlAnimalCodeAllocator(GenSWDbContext dbContext) : IAnimalCodeAllocator
{
    public async Task<IAnimalAutomaticCodeAttempt> BeginAttemptAsync(
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is {} outer)
        {
            var savepoint = "animal_code_" + Guid.NewGuid().ToString("N");
            await outer.CreateSavepointAsync(savepoint, cancellationToken);
            return new NestedCodeAttempt(dbContext, outer, savepoint);
        }
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new AnimalAutomaticCodeAttempt(dbContext, transaction);
    }

    // A conversion owns the outer transaction; automatic-code retries use a
    // savepoint so creating Animal cannot commit the conversion prematurely.
    private sealed class NestedCodeAttempt(GenSWDbContext context, IDbContextTransaction outer, string savepoint)
        : IAnimalAutomaticCodeAttempt
    {
        private bool completed;
        public async Task<string> AllocateNextCodigoInternoAsync(CancellationToken ct = default)
        {
            if (completed) throw new InvalidOperationException("Automatic-code attempt is closed.");
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.Transaction = outer.GetDbTransaction();
            command.CommandText = "SELECT nextval('\"AnimalCodigoInternoSequence\"');";
            try
            {
                var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
                return $"AN-{value.ToString("D6", CultureInfo.InvariantCulture)}";
            }
            catch (PostgresException e) when (e.SqlState == "2200H") { throw new AnimalCodeSequenceExhaustedException(); }
        }
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (completed) throw new InvalidOperationException("Automatic-code attempt is closed.");
            await outer.ReleaseSavepointAsync(savepoint, ct);
            completed = true;
        }
        public async Task RollbackAndDetachAsync(Animal failedAnimal, CancellationToken ct = default)
        {
            await RollbackAsync(ct);
            context.Entry(failedAnimal).State = EntityState.Detached;
        }
        private async Task RollbackAsync(CancellationToken ct = default)
        {
            if (completed) return;
            await outer.RollbackToSavepointAsync(savepoint, ct);
            await outer.ReleaseSavepointAsync(savepoint, ct);
            completed = true;
        }
        public async ValueTask DisposeAsync() => await RollbackAsync();
    }

    private sealed class AnimalAutomaticCodeAttempt(
        GenSWDbContext dbContext,
        IDbContextTransaction transaction) : IAnimalAutomaticCodeAttempt
    {
        private bool commitCompleted;
        private bool rollbackCompleted;
        private bool transactionDisposalAttempted;

        public async Task<string> AllocateNextCodigoInternoAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureOpen();

            try
            {
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.Transaction = transaction.GetDbTransaction();
                command.CommandText = "SELECT nextval('\"AnimalCodigoInternoSequence\"');";
                var sequenceValue = Convert.ToInt64(
                    await command.ExecuteScalarAsync(cancellationToken),
                    CultureInfo.InvariantCulture);
                return $"AN-{sequenceValue.ToString("D6", CultureInfo.InvariantCulture)}";
            }
            catch (PostgresException exception) when (exception.SqlState == "2200H")
            {
                throw new AnimalCodeSequenceExhaustedException();
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            EnsureOpen();

            await transaction.CommitAsync(cancellationToken);
            commitCompleted = true;
            await DisposeTransactionAsync();
        }

        public async Task RollbackAndDetachAsync(
            Animal failedAnimal,
            CancellationToken cancellationToken = default)
        {
            EnsureOpen();

            await transaction.RollbackAsync(cancellationToken);
            rollbackCompleted = true;

            Exception? detachFailure = null;
            try
            {
                dbContext.Entry(failedAnimal).State = EntityState.Detached;
            }
            catch (Exception exception)
            {
                detachFailure = exception;
            }

            Exception? disposalFailure = null;
            try
            {
                await DisposeTransactionAsync();
            }
            catch (Exception exception)
            {
                disposalFailure = exception;
            }

            ThrowCleanupFailures(detachFailure, disposalFailure);
        }

        public async ValueTask DisposeAsync()
        {
            if (transactionDisposalAttempted)
            {
                return;
            }

            Exception? rollbackFailure = null;
            if (!commitCompleted && !rollbackCompleted)
            {
                try
                {
                    await transaction.RollbackAsync();
                    rollbackCompleted = true;
                }
                catch (Exception exception)
                {
                    rollbackFailure = exception;
                }
            }

            Exception? disposalFailure = null;
            try
            {
                await DisposeTransactionAsync();
            }
            catch (Exception exception)
            {
                disposalFailure = exception;
            }

            ThrowCleanupFailures(rollbackFailure, disposalFailure);
        }

        private void EnsureOpen()
        {
            if (commitCompleted || rollbackCompleted || transactionDisposalAttempted)
            {
                throw new InvalidOperationException("The animal code allocation attempt has already completed.");
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (transactionDisposalAttempted)
            {
                return;
            }

            transactionDisposalAttempted = true;
            await transaction.DisposeAsync();
        }

        private static void ThrowCleanupFailures(Exception? primaryFailure, Exception? cleanupFailure)
        {
            if (primaryFailure is not null && cleanupFailure is not null)
            {
                throw new AggregateException(primaryFailure, cleanupFailure);
            }

            if (primaryFailure is not null)
            {
                ExceptionDispatchInfo.Capture(primaryFailure).Throw();
            }

            if (cleanupFailure is not null)
            {
                ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
            }
        }
    }
}
