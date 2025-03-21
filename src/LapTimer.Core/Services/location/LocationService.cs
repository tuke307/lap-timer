﻿namespace LapTimer.Core.Services
{
    using LapTimer.Core.Models;
    using Microsoft.Extensions.Logging;
    using MvvmCross.Logging;
    using MvvmCross.Plugin.Location;
    using MvvmCross.Plugin.Messenger;
    using System;

    /// <summary>
    /// LocationService.
    /// </summary>
    /// <seealso cref="LapTimer.Core.Services.ILocationService" />
    public class LocationService
       : ILocationService
    {
        private readonly ILogger _log;
        private readonly IMvxMessenger _messenger;
        private readonly IMvxLocationWatcher _watcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocationService" /> class.
        /// </summary>
        /// <param name="watcher">The watcher.</param>
        /// <param name="messenger">The messenger.</param>
        /// <param name="log">The log.</param>
        public LocationService(IMvxLocationWatcher watcher, IMvxMessenger messenger, ILogger log)
        {
            this._watcher = watcher;
            this._messenger = messenger;
            this._log = log;

            var options = new MvxLocationOptions
            {
                Accuracy = MvxLocationAccuracy.Fine,
                TrackingMode = MvxLocationTrackingMode.Foreground,
                TimeBetweenUpdates = TimeSpan.Zero,
                MovementThresholdInM = 0,
            };

            this._watcher.Start(options, this.OnLocation, this.OnError);
        }

        /// <summary>
        /// Called when [error].
        /// </summary>
        /// <param name="error">The error.</param>
        private void OnError(MvxLocationError error)
        {
            this._log.LogError($"ERROR: Location Error: {0}", error.Code);
        }

        /// <summary>
        /// Called when [location].
        /// </summary>
        /// <param name="location">The location.</param>
        private void OnLocation(MvxGeoLocation location)
        {
            var message = new MvxLocationMessage(this,
                                                 location.Coordinates);

            this._messenger.Publish(message);
        }
    }
}