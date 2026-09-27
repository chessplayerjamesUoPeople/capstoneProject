using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmigoAlert.Data
{
	/// <summary>
	/// Begins database transactions that span several repository calls.
	/// </summary>
	/// <remarks>
	/// Repositories write with <c>ExecuteUpdateAsync</c>, which executes immediately and ignores <c>SaveChangesAsync</c>, so a service that
	/// changes more than one table must wrap those calls in an explicit transaction. Every repository in a DI scope shares the one scoped
	/// <see cref="AmigoAlertDbContext"/>, so a transaction begun here covers all of them.
	/// </remarks>
	public interface ITransactionProvider
	{
		/// <summary>
		/// Begins a transaction on the scoped context. Disposing it without calling <c>CommitAsync</c> rolls it back.
		/// </summary>
		/// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>The transaction, which the caller owns and must dispose.</returns>
		Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
	}
}
