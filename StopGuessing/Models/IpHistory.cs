﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using StopGuessing.DataStructures;

namespace StopGuessing.Models
{
    /// <summary>
    /// This class keeps track of recent login successes and failures for a given client IP so that
    /// we can try to determine if this client should be blocked due to likely-password-guessing
    /// behaviors.
    /// </summary>
    public class IpHistory
    {
        public IPAddress Address;
        /// <summary>
        /// Past login successes, ordered from most recent to least recent, with a fixed limit on the history stored.
        /// </summary>
        public Sequence<LoginAttempt> RecentLoginSuccessesAtMostOnePerAccount;
        const int DefaultNumberOfLoginSuccessesToTrack = 32;

        /// <summary>
        /// Past login failures, ordered from most recent to least recent, with a fixed limit on the history stored.
        /// </summary>
        public Sequence<LoginAttempt> RecentLoginFailures;
        const int DefaultNumberOfLoginFailuresToTrack = 32;


        public IpHistory(//bool isIpAKnownAggregatorThatWeCannotBlock = false,
            IPAddress address,
                                 int numberOfLoginSuccessesToTrack = DefaultNumberOfLoginSuccessesToTrack,
                                 int numberOfLoginFailuresToTrack = DefaultNumberOfLoginFailuresToTrack)
        {
            //this.IsIpAKnownAggregatorThatWeCannotBlock = isIpAKnownAggregatorThatWeCannotBlock;
            Address = address;
            RecentLoginSuccessesAtMostOnePerAccount =
                new Sequence<LoginAttempt>(numberOfLoginSuccessesToTrack);
            RecentLoginFailures =
                new Sequence<LoginAttempt>(numberOfLoginFailuresToTrack);
        }


        public void RecordLoginAttempt(LoginAttempt attempt)
        {
            //Record login attempts
            if (attempt.Outcome == AuthenticationOutcome.CredentialsValid ||
                attempt.Outcome == AuthenticationOutcome.CredentialsValidButBlocked)
            {
                // If there was a prior success from the same account, remove it, as we only need to track
                // successes to counter failures and we only counter failures once per account.
                LoginAttempt previousAttemptFromSameAccount =
                    RecentLoginSuccessesAtMostOnePerAccount.FirstOrDefault(la => la.UsernameOrAccountId == attempt.UsernameOrAccountId);
                if (previousAttemptFromSameAccount != null)
                {
                    // We found a prior success from the same account.  Remove it.
                    RecentLoginSuccessesAtMostOnePerAccount.Remove(previousAttemptFromSameAccount);
                }

                RecentLoginSuccessesAtMostOnePerAccount.Add(attempt);
            }
            else
            {
                RecentLoginFailures.Add(attempt);
            }
        }


        /// <summary>
        /// Update LoginAttempts cached for this IP with new outcomes
        /// </summary>
        /// <param name="changedLoginAttempts">Copies of the login attempts that have changed</param>
        /// <returns></returns>
        public int UpdateLoginAttemptsWithNewOutcomes(IEnumerable<LoginAttempt> changedLoginAttempts)
        {
            // Fot the attempts provided in the paramters, create a dictionary mapping the attempt keys to
            // the attempt so that we can look them up quickly when going through the recent failures
            // for this IP.
            Dictionary<string, LoginAttempt> keyToChangedAttempts = new Dictionary<string, LoginAttempt>();
            foreach (LoginAttempt attempt in changedLoginAttempts)
            {
                keyToChangedAttempts[attempt.UniqueKey] = attempt;
            }

            // Now walk through the failures for this IP and, if any match the keys for the
            // accounts in the changedLoginAttempts parameter, change the outcome to match the 
            // one provided.
            List<LoginAttempt> attemptsThatNeedToBeUpdated =
                RecentLoginFailures.MostRecentToOldest.Where(
                    attempt => keyToChangedAttempts.ContainsKey(attempt.UniqueKey)).ToList();
            foreach (LoginAttempt attemptThatNeedsToBeUpdated in attemptsThatNeedToBeUpdated)
            {
                LoginAttempt changedAttempt = keyToChangedAttempts[attemptThatNeedsToBeUpdated.UniqueKey];
                // This is where we update the attempt that needs to be updated with the outcome in the attempt that's already
                // been changed to include the latest outcome.
                attemptThatNeedsToBeUpdated.Outcome = changedAttempt.Outcome;
            }
            return attemptsThatNeedToBeUpdated.Count;
        }


    }
}
