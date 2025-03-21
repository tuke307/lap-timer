﻿namespace LapTimer.Forms.UI.ViewModels.LapTimer
{
    using global::LapTimer.Core.Services;
    using global::LapTimer.Forms.UI.Models;
    using global::LapTimer.Forms.UI.Services;
    using Microsoft.Extensions.Logging;
    using MvvmCross;
    using MvvmCross.Commands;
    using MvvmCross.Logging;
    using MvvmCross.Navigation;
    using MvvmCross.Plugin.Messenger;
    using MvvmCross.ViewModels;
    using System;
    using System.Threading.Tasks;
    using System.Timers;
    using Xamarin.Essentials;

    /// <summary>
    /// CountdownViewModel.
    /// </summary>
    /// <seealso cref="MvvmCross.ViewModels.MvxNavigationViewModel" />
    public class CountdownViewModel : MvxNavigationViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LapTimerTabViewModel" /> class.
        /// </summary>
        /// <param name="logFactory">The log provider.</param>
        /// <param name="navigationService">The navigation service.</param>
        public CountdownViewModel(ILoggerFactory logFactory, IMvxNavigationService navigationService, IMvxMessenger messenger, IRideService rideService, ICountdownService countdownTimer)
            : base(logFactory, navigationService)
        {
            _countdownTimer = countdownTimer;
            _messenger = messenger;
            StartLapTimerCommand = new MvxCommand(() => _messenger.Publish(new MvxTabIndexMessenger(this, 3)));
            ExtendCountdownCommand = new MvxCommand(ExtendCountdown);
        }

        #region Methods

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        /// <returns>Initialisierung.</returns>
        public override Task Initialize()
        {
            return base.Initialize();
        }

        /// <summary>
        /// Prepares this instance. called after construction.
        /// </summary>
        public override void Prepare()
        {
            base.Prepare();
        }

        public override void ViewAppeared()
        {
            base.ViewAppeared();

            //start timer after navigating
            _countdownTimer.Ticked += CountdownTimerTicked;
            _countdownTimer.Completed += CountdownTimerCompleted;

            // aller 1/10 sekunde wird zeit aktualisiert
            _countdownTimer.Start(TimeSpan.FromSeconds(timerDuration), TimeSpan.FromMilliseconds(100));
        }

        private void CountdownTimerCompleted(object sender, EventArgs e)
        {
            _countdownTimer.Ticked -= CountdownTimerTicked;
            _countdownTimer.Completed -= CountdownTimerCompleted;
            StartLapTimerCommand.Execute();
            Vibration.Vibrate(TimeSpan.FromSeconds(vibrationDuration));
        }

        private void CountdownTimerTicked(object sender, TimerEventArgs e)
        {
            TimeSpanCountdown = e.TimeRemaining;
        }

        private void ExtendCountdown()
        {
            _countdownTimer.Extend(TimeSpan.FromSeconds(extendTimerDuration));
        }

        #endregion Methods

        #region Values

        #region Commands

        public IMvxCommand ExtendCountdownCommand { get; protected set; }

        public IMvxCommand StartLapTimerCommand { get; protected set; }

        #endregion Commands

        private static double extendTimerDuration = 10.0;
        private static double timerDuration = 15.0;
        private static double vibrationDuration = 1.0;
        private readonly ICountdownService _countdownTimer;
        private readonly IMvxMessenger _messenger;
        private TimeSpan _timeSpanCountdown;

        public TimeSpan TimeSpanCountdown
        {
            get => this._timeSpanCountdown;
            set => this.SetProperty(ref _timeSpanCountdown, value);
        }

        #endregion Values
    }
}