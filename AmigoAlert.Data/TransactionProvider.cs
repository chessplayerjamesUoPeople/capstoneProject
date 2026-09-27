using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data
{
	/// <inheritdoc cref="ITransactionProvider"/>
	internal sealed class TransactionProvider(AmigoAlertDbContext context) : ITransactionProvider
	{
		/// <inheritdoc cref="ITransactionProvider.BeginTransactionAsync"/>
		public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
		{
			return context.Database.BeginTransactionAsync(ct);
		}
	}
}
