﻿using LanguageExt;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using static LanguageExt.Prelude;

namespace Isotope80
{
    public delegate IsotopeState<A> Isotope<A>(IsotopeState state);

    public static partial class Isotope
    {
        /// <summary>
        /// Run the test computation - returning an optional error. 
        /// The computation succeeds if result.IsNone is true
        /// </summary>
        /// <param name="ma">Test computation</param>
        public static (IsotopeState state, A value) Run<A>(this Isotope<A> ma, IsotopeSettings settings = null)
        {
            var res = ma(IsotopeState.Empty.With(Settings: settings));

            if (res.State.Settings.DisposeOnCompletion)
            {
                res.State.DisposeWebDriver();
            }

            return(res.State, res.Value);
        }

        public static (IsotopeState state, A value) Run<A>(this Isotope<A> ma, IWebDriver driver, IsotopeSettings settings = null)
        {
            var res = ma(IsotopeState.Empty.With(Driver: Some(driver), Settings: settings));

            if (res.State.Settings.DisposeOnCompletion)
            {
                res.State.DisposeWebDriver();
            }

            return (res.State, res.Value);
        }

        /// <summary>
        /// Run the test computation - throws and error if it fails to pass
        /// </summary>
        /// <param name="ma">Test computation</param>
        public static (IsotopeState state, A value) RunAndThrowOnError<A>(this Isotope<A> ma, IWebDriver driver, IsotopeSettings settings = null)
        {
            var res = ma(IsotopeState.Empty.With(Driver: Some(driver), Settings: settings));

            if (res.State.Settings.DisposeOnCompletion)
            {
                res.State.DisposeWebDriver();
            }

            res.State.Error.Match(
                Some: x => { res.State.Settings.FailureAction(x, res.State.Log); return failwith<Unit>(x); },
                None: () => unit);

            return (res.State, res.Value);
        }

        /// <summary>
        /// Simple configuration setup
        /// </summary>
        /// <param name="config">Map of config items</param>
        public static Isotope<Unit> initConfig(params (string, string)[] config) =>
            initConfig(toMap(config));

        /// <summary>
        /// Simple configuration setup
        /// </summary>
        /// <param name="config">Map of config items</param>
        public static Isotope<Unit> initConfig(Map<string, string> config) =>
            from s in get
            from _ in put(s.With(Configuration: config))
            select unit;

        /// <summary>
        /// Get a config key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Isotope<string> config(string key) =>
            from s in get
            from r in s.Configuration.Find(key).ToIsotope($"Configuration key not found: {key}")
            select r;

        public static Isotope<Unit> initSettings(IsotopeSettings settings) =>
            from s in get
            from _ in put(s.With(Settings: settings))
            select unit;

        public static Isotope<Unit> setWindowSize(int width, int height) =>
            from d in webDriver
            let size = new System.Drawing.Size(width, height)
            from _ in trya(() => d.Manage().Window.Size = size, $"Failed to change browser window size to {width} by {height}")
            select unit;

        /// <summary>
        /// Navigate to a URL
        /// </summary>
        /// <param name="url">URL to navigate to</param>
        public static Isotope<Unit> nav(string url) =>
            from d in webDriver
            from _ in trya(() => d.Navigate().GoToUrl(url), $"Failed to navigate to: {url}")
            select unit;

        /// <summary>
        /// Gets the URL currently displayed by the browser
        /// </summary>
        public static Isotope<string> url =>
            from d in webDriver
            select d.Url;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">CSS selector</param>
        public static Isotope<IWebElement> findElement(By selector, bool wait = true, string errorMessage = "Unable to find element") =>
            from d in webDriver
            from e in wait ? waitUntilElementExists(selector) : fail<IWebElement>(errorMessage)
            select e;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">Selector</param>
        public static Isotope<Option<IWebElement>> findOptionalElement(IWebElement element, By selector, string errorMessage = null) =>
            from es in findElementsOrEmpty(element, selector, errorMessage)
            from e in pure(es.HeadOrNone())
            select e;

        public static Isotope<Option<IWebElement>> findOptionalElement(By selector, string errorMessage = null) =>
            from es in findElementsOrEmpty(selector, errorMessage)
            from e in pure(es.HeadOrNone())
            select e;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">CSS selector</param>
        public static Isotope<IWebElement> findElement(
            IWebElement element, 
            By selector, 
            bool wait = true, 
            string errorMessage = null) =>
            from d in webDriver
            from e in wait 
                      ? waitUntilElementExists(element, selector)
                      : findChildElement(element, selector) 
            select e;

        private static Isotope<IWebElement> findChildElement(
            IWebElement parent,
            By selector,
            string errorMessage = null) =>
            tryf(() => parent.FindElement(selector),
                 errorMessage ?? $"Can't find element {selector}");

        /// <summary>
        /// Find HTML elements
        /// </summary>
        /// <param name="selector">Selector</param>
        public static Isotope<Seq<IWebElement>> findElements(By selector, bool wait = true, string error = null) =>
            wait ? waitUntilElementsExists(selector)
                 : from d in webDriver
                   from es in tryf(() => d.FindElements(selector).ToSeq(),
                                   error ?? $"Can't find any elements {selector}")
                   select es;

        /// <summary>
        /// Find HTML elements within an element
        /// </summary>
        /// <param name="parent">Element to search within</param>
        /// <param name="selector">Selector</param>
        /// <param name="wait">If none are found wait and retry</param>
        /// <param name="error">Custom error message</param>
        /// <returns>Matching elements</returns>
        public static Isotope<Seq<IWebElement>> findElements(IWebElement parent, By selector, bool wait = true, string error = null) =>
            wait ? waitUntilElementsExists(parent, selector)
                 : Try(() => parent.FindElements(selector).ToSeq()).
                    Match(
                     Succ: x => x.IsEmpty ? fail<Seq<IWebElement>>(error ?? $"Can't find any elements {selector}")
                                          : pure(x),
                     Fail: e => fail<Seq<IWebElement>>(error ?? $"Can't find any elements {selector}"));

        /// <summary>
        /// Find a sequence of elements matching a selector
        /// </summary>
        /// <param name="selector">Web Driver selector</param>
        /// <param name="error"></param>
        /// <returns>Sequence of matching elements</returns>
        public static Isotope<Seq<IWebElement>> findElementsOrEmpty(By selector, string error = null) =>
            from d in webDriver
            from e in tryf(() => d.FindElements(selector).ToSeq(), error ?? $"Can't find any elements {selector}")
            select e;

        /// <summary>
        /// Find a sequence of elements within an existing element matching a selector
        /// </summary>
        /// <param name="parent">Parent element</param>
        /// <param name="selector">Web Driver selector</param>
        /// <param name="error"></param>
        /// <returns>Sequence of matching elements</returns>
        public static Isotope<Seq<IWebElement>> findElementsOrEmpty(IWebElement parent, By selector, string error = null) =>
            from e in tryf(() => parent.FindElements(selector).ToSeq(), error ?? $"Can't find any elements {selector}")
            select e;

        /// <summary>
        /// Find a &lt;select&gt; element within an existing element
        /// </summary>  
        public static Isotope<SelectElement> findSelectElement(IWebElement container, By selector) =>
            from el in findElement(container, selector)
            from se in toSelectElement(el)
            select se;

        /// <summary>
        /// Find a &lt;select&gt; element
        /// </summary>        
        public static Isotope<SelectElement> findSelectElement(By selector) =>
            from el in findElement(selector)
            from se in toSelectElement(el)
            select se;

        /// <summary>
        /// Convert an IWebElement to a SelectElement
        /// </summary>  
        public static Isotope<SelectElement> toSelectElement(IWebElement element) =>
            tryf(() => new SelectElement(element), x => "Problem creating select element: " + x.Message);

        /// <summary>
        /// Select a &lt;select&gt; option by text
        /// </summary>     
        public static Isotope<Unit> selectByText(By selector, string text) =>
            from se in findSelectElement(selector)
            from _  in selectByText(se, text)
            select unit;

        /// <summary>
        /// Select a &lt;select&gt; option by text
        /// </summary>        
        public static Isotope<Unit> selectByText(SelectElement select, string text) =>
            trya(() => select.SelectByText(text), x => "Unable to select" + x.Message);

        /// <summary>
        /// Select a &lt;select&gt; option by value
        /// </summary>     
        public static Isotope<Unit> selectByValue(By selector, string value) =>
            from se in findSelectElement(selector)
            from _  in selectByValue(se, value)
            select unit;

        /// <summary>
        /// Select a &lt;select&gt; option by value
        /// </summary>        
        public static Isotope<Unit> selectByValue(SelectElement select, string value) =>
            trya(() => select.SelectByValue(value), x => "Unable to select" + x.Message);

        /// <summary>
        /// Retrieves the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<IWebElement> getSelectedOption(SelectElement sel) =>
            tryf(() => sel.SelectedOption, x => "Unable to get selected option" + x.Message);

        /// <summary>
        /// Retrieves the text for the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<string> getSelectedOptionText(SelectElement sel) =>
            from opt in getSelectedOption(sel)
            from txt in text(opt)
            select txt;

        /// <summary>
        /// Retrieves the value for the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<string> getSelectedOptionValue(SelectElement sel) =>
            from opt in getSelectedOption(sel)
            from val in value(opt)
            select val;

        /// <summary>
        /// Finds a checkbox element by selector and identifies whether it is checked
        /// </summary>
        /// <param name="selector">Web Driver Selector</param>
        /// <returns>Is checked\s</returns>
        public static Isotope<bool> isCheckboxChecked(By selector) =>
            from el in findElement(selector)
            from res in isCheckboxChecked(el)
            select res;

        /// <summary>
        /// Identifies whether an existing checkbox is checked
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <returns>Is checked\s</returns>
        public static Isotope<bool> isCheckboxChecked(IWebElement el) =>
            pure(el.Selected);

        /// <summary>
        /// Set checkbox value for existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="ticked">Check the box or not</param>
        /// <returns>Unit</returns>
        public static Isotope<Unit> setCheckbox(IWebElement el, bool ticked) =>
            from val in isCheckboxChecked(el)
            from _   in val == ticked
                        ? pure(unit)
                        : click(el)
            select unit;

        /// <summary>
        /// Looks for a particular style attribute on an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="style">Style attribute to look up</param>
        /// <returns>A string representing the style value</returns>
        public static Isotope<string> getStyle(IWebElement el, string style) =>
            tryf(() => el.GetCssValue(style), $"Could not find style {style}");

        /// <summary>
        /// Gets the Z Index style attribute value for an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <returns>The Z Index value</returns>
        public static Isotope<int> getZIndex(IWebElement el) =>
            from zis in getStyle(el, "zIndex")
            from zii in parseInt(zis).ToIsotope($"z-Index was not valid integer: {zis}.")
            select zii;

        /// <summary>
        /// Looks for a particular style attribute on an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="att">Attribute to look up</param>
        /// <returns>A string representing the attribute value</returns>
        public static Isotope<string> attribute(IWebElement el, string att) =>
            tryf(() => el.GetAttribute(att), $"Attribute {att} could not be found.");

        /// <summary>
        /// Simulates keyboard by sending `keys` 
        /// </summary>
        /// <param name="selector">Selector for element to type into</param>
        /// <param name="keys">String of characters that are typed</param>
        public static Isotope<Unit> sendKeys(By selector, string keys) =>
            from el in findElement(selector)
            from _  in sendKeys(el, keys)
            select unit;

        /// <summary>
        /// Simulates keyboard by sending `keys` 
        /// </summary>
        /// <param name="element">Element to type into</param>
        /// <param name="keys">String of characters that are typed</param>
        public static Isotope<Unit> sendKeys(IWebElement element, string keys) =>
            trya(() => element.SendKeys(keys), $@"Error sending keys ""{keys}"" to element: {element.PrettyPrint()}");

        /// <summary>
        /// Simulates the mouse-click
        /// </summary>
        /// <param name="selector">Web Driver Selector</param>
        /// <returns>Unit</returns>
        public static Isotope<Unit> click(By selector) =>
            from el in findElement(selector)
            from _ in click(el)
            select unit;

        /// <summary>
        /// Simulates the mouse-click
        /// </summary>
        /// <param name="element">Element to click</param>
        public static Isotope<Unit> click(IWebElement element) =>
            trya(() => element.Click(), $@"Error clicking element: {element.PrettyPrint()}");

        /// <summary>
        /// Clears the content of an element
        /// </summary>
        /// <param name="element">Web Driver Element</param>
        /// <returns>Unit</returns>
        public static Isotope<Unit> clear(IWebElement element) =>
            trya(() => element.Clear(), $@"Error clearing element: {element.PrettyPrint()}");

        /// <summary>
        /// ONLY USE AS A LAST RESORT
        /// Pauses the processing for an interval to brute force waiting for actions to complete
        /// </summary>
        public static Isotope<Unit> pause(TimeSpan interval)
        {
            Thread.Sleep((int)interval.TotalMilliseconds);
            return pure(unit);
        }

        /// <summary>
        /// Gets the text inside an element
        /// </summary>
        /// <param name="element">Element containing txt</param>
        public static Isotope<string> text(IWebElement element) =>
            tryf(() => element.Text, $@"Error getting text from element: {element.PrettyPrint()}");

        /// <summary>
        /// Gets the value attribute of an element
        /// </summary>
        /// <param name="element">Element containing value</param>
        public static Isotope<string> value(IWebElement element) =>
            tryf(() => element.GetAttribute("Value"), $@"Error getting value from element: {element.PrettyPrint()}");

        /// <summary>
        /// Web driver accessor
        /// </summary>
        public static Isotope<IWebDriver> webDriver =>
            from s in get
            from r in s.Driver.ToIsotope("web-driver hasn't been selected yet")
            select r;

        /// <summary>
        /// Web driver setter
        /// </summary>
        public static Isotope<Unit> setWebDriver(IWebDriver d) =>
            from s in get
            from _ in put(s.With(Driver: Some(d)))
            select unit;

        public static Isotope<Unit> disposeWebDriver =>
            from s in get
            select s.DisposeWebDriver();

        /// <summary>
        /// Default wait accessor
        /// </summary>
        public static Isotope<TimeSpan> defaultWait =>
            from s in get
            select s.Settings.Wait;

        /// <summary>
        /// Default wait accessor
        /// </summary>
        public static Isotope<TimeSpan> defaultInterval =>
            from s in get
            select s.Settings.Interval;

        /// <summary>
        /// Identity - lifts a value of `A` into the Isotope monad
        /// 
        /// * Always succeeds *
        /// 
        /// </summary>
        public static Isotope<A> pure<A>(A value) =>
            state =>
                new IsotopeState<A>(value, state);

        /// <summary>
        /// Useful for starting a linq expression if you need lets first
        /// i.e.
        ///         from _ in unitM
        ///         let foo = "123"
        ///         let bar = "456"
        ///         from x in ....
        /// </summary>
        public static Isotope<Unit> unitM => pure(unit);

        /// <summary>
        /// Failure - creates an Isotope monad that always fails
        /// </summary>
        /// <param name="message">Error message</param>
        public static Isotope<A> fail<A>(string message) =>
            state =>
                new IsotopeState<A>(default, state.With(Error: Some(message)));

        /// <summary>
        /// Gets the state from the Isotope monad
        /// </summary>
        public static Isotope<IsotopeState> get =
            state =>
                new IsotopeState<IsotopeState>(state, state);

        /// <summary>
        /// Puts the state back into the Isotope monad
        /// </summary>
        public static Isotope<Unit> put(IsotopeState state) =>
            _ =>
                new IsotopeState<Unit>(unit, state);

        /// <summary>
        /// Try and action
        /// </summary>
        /// <param name="action">Action to try</param>
        /// <param name="label">Error string if exception is thrown</param>
        /// <returns></returns>
        public static Isotope<Unit> trya(Action action, string label) =>
            Try(() => { action(); return unit; }).ToIsotope(label);

        /// <summary>
        /// Try an action
        /// </summary>
        /// <param name="action">Action to try</param>
        /// <param name="makeError">Convert Exception to an error string</param>
        /// <returns></returns>
        public static Isotope<Unit> trya(Action action, Func<Exception, string> makeError) =>
            Try(() => { action(); return unit; }).ToIsotope(makeError);

        /// <summary>
        /// Try a function
        /// </summary>
        /// <typeparam name="A">Return type of the function</typeparam>
        /// <param name="func">Function to try</param>
        /// <param name="label">Error string if exception is thrown</param>
        /// <returns></returns>
        public static Isotope<A> tryf<A>(Func<A> func, string label) =>
            Try(() => func()).ToIsotope(label);

        /// <summary>
        /// Try a function
        /// </summary>
        /// <typeparam name="A">Return type of the function</typeparam>
        /// <param name="func">Function to try</param>
        /// <param name="makeError">Convert Exception to an error string</param>
        /// <returns>The result of the function</returns>
        public static Isotope<A> tryf<A>(Func<A> func, Func<Exception, string> makeError) =>
            Try(() => func()).ToIsotope(makeError);

        /// <summary>
        /// Run a void returning action
        /// </summary>
        /// <param name="action">Action to run</param>
        /// <returns>Unit</returns>
        public static Isotope<Unit> voida(Action action) => state =>
        {
            action();
            return new IsotopeState<Unit>(unit, state);
        };

        /// <summary>
        /// Log some output
        /// </summary>
        public static Isotope<Unit> log(string message) =>
            from st in get
            from _1 in put(st.Write(message, st.Settings.LoggingAction))
            select unit;

        public static Isotope<Unit> pushLog(string message) =>
            from st in get
            from _1 in put(st.PushLog(message, st.Settings.LoggingAction))
            select unit;

        public static Isotope<Unit> popLog =>
            from st in get
            from _1 in put(st.PopLog())
            select unit;

        public static Isotope<A> context<A>(string context, Isotope<A> iso) =>
            from _1 in pushLog(context)
            from re in iso
            from _2 in popLog
            select re;

        public static Isotope<Seq<IWebElement>> waitUntilElementsExists(
            By selector,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from el in waitUntil(findElementsOrEmpty(selector), x => x.IsEmpty, interval: interval, wait: wait)
            select el;

        public static Isotope<Seq<IWebElement>> waitUntilElementsExists(
            IWebElement parent,
            By selector,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from el in waitUntil(findElementsOrEmpty(parent, selector), x => x.IsEmpty, interval: interval, wait: wait)
            select el;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selector"></param>
        /// <param name="interval"></param>
        /// <param name="wait"></param>
        /// <returns></returns>
        public static Isotope<IWebElement> waitUntilElementExists(
            By selector, 
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from x in waitUntil(
                            findOptionalElement(selector),
                            el => el.IsNone,
                            interval,
                            wait)
            from y in x.Match(
                            Some: s => pure(s),
                            None: () => fail<IWebElement>("Element not found within timeout period"))
            select y;

        /// <summary>
        /// Attempts to find a child element within an existing element and if not present retries for a period.
        /// </summary>
        /// <param name="element">Parent element</param>
        /// <param name="selector">Selector within element</param>
        /// <param name="interval">The time period between attempts to check, if not provided the default value from Settings is used.</param>
        /// <param name="wait">The overall time period to attempt for, if not provided the default value from Settings is used.</param>
        /// <returns></returns>
        public static Isotope<IWebElement> waitUntilElementExists(
            IWebElement element, 
            By selector, 
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from x in waitUntil(
                            findOptionalElement(element, selector),
                            el => el.IsNone,
                            interval,
                            wait)
            from y in x.Match(
                            Some: s => pure(s),
                            None: () => fail<IWebElement>("Element not found within timeout period"))
            select y;

        /// <summary>
        /// Wait for an element to be rendered and clickable, fail if exceeds default timeout
        /// </summary>
        public static Isotope<IWebElement> waitUntilClickable(By selector) =>
            from w  in defaultWait
            from el in waitUntilClickable(selector, w)
            select el;

        /// <summary>
        /// Wait for an element to be rendered and clickable, fail if exceeds default timeout
        /// </summary>
        public static Isotope<Unit> waitUntilClickable(IWebElement element) =>
            from w in defaultWait
            from _ in waitUntilClickable(element, w)
            select unit;

        public static Isotope<IWebElement> waitUntilClickable(By selector, TimeSpan timeout) =>
            from _1 in log($"Waiting until clickable: {selector}")
            from el in waitUntilElementExists(selector)
            from _2 in waitUntilClickable(el, timeout)
            select el;

        public static Isotope<Unit> waitUntilClickable(IWebElement el, TimeSpan timeout) =>
            from _ in waitUntil(
                        from _1a in log($"Checking clickability " + el.PrettyPrint())
                        from d in displayed(el)
                        from e in enabled(el)
                        from o in obscured(el)
                        from _2a in log($"Displayed: {d}, Enabled: {e}, Obscured: {o}")
                        select d && e && (!o),
                        x => !x)
            select unit;

        public static string PrettyPrint(this IWebElement x)
        {
            var tag = x.TagName;
            var css = x.GetAttribute("class");
            var id = x.GetAttribute("id");

            return $"<{tag} class='{css}' id='{id}'>";
        }

        /// <summary>
        /// Flips the sequence of Isotopes to be a Isotope of Sequences
        /// </summary>
        /// <typeparam name="A"></typeparam>
        /// <param name="mas"></param>
        /// <returns></returns>
        public static Isotope<Seq<A>> Sequence<A>(this Seq<Isotope<A>> mas) => state =>
        {
            var rs = new A[mas.Count];
            int index = 0;

            foreach (var ma in mas)
            {
                var s = ma(state);
                if (s.State.Error.IsSome)
                {
                    return new IsotopeState<Seq<A>>(default, s.State);
                }

                state = s.State;
                rs[index] = s.Value;
                index++;
            }
            return new IsotopeState<Seq<A>>(rs.ToSeq(), state);
        };

        /// <summary>
        /// Flips the sequence of Isotopes to be a Isotope of Sequences
        /// </summary>
        /// <typeparam name="A"></typeparam>
        /// <param name="mas"></param>
        /// <returns></returns>
        public static Isotope<Seq<A>> Collect<A>(this Seq<Isotope<A>> mas) => state =>
        {
            if(state.Error.IsSome)
            {
                return new IsotopeState<Seq<A>>(default, state);
            }

            var rs = new A[mas.Count];
            int index = 0;

            // Create an empty log TODO
            //var logs = state.Log.Cons(Seq<Seq<string>>());

            // Clear log from the state
            state = state.With(Log: Log.Empty);

            bool hasFaulted = false;
            var errors = new List<string>();

            foreach (var ma in mas)
            {
                var s = ma(state);

                // Collect error
                hasFaulted = hasFaulted || s.State.Error.IsSome;
                if(s.State.Error.IsSome)
                {
                    errors.Add((string)s.State.Error);
                }

                // Collect logs TODO
                //logs = logs.Add(s.State.Log);

                // Record value
                rs[index] = s.Value;
                index++;
            }

            return new IsotopeState<Seq<A>>(rs.ToSeq(), state.With(
                Error: hasFaulted
                            ? Some(String.Join(" | ", errors))
                            : None,
                Log: Log.Empty));//LanguageExt.Seq.flatten(logs)));
        };

        public static Isotope<B> Select<A, B>(this Isotope<A> ma, Func<A, B> f) => sa =>
        {
            var a = ma(sa);
            if (a.State.Error.IsSome) return new IsotopeState<B>(default(B), a.State);
            return new IsotopeState<B>(f(a.Value), a.State);
        };

        public static Isotope<B> Map<A, B>(this Isotope<A> ma, Func<A, B> f) => ma.Select(f);

        public static Isotope<B> Bind<A, B>(this Isotope<A> ma, Func<A, Isotope<B>> f) => SelectMany(ma, f);

        public static Isotope<B> SelectMany<A, B>(this Isotope<A> ma, Func<A, Isotope<B>> f) => sa =>
        {
            if (sa.Error.IsSome) return new IsotopeState<B>(default, sa);

            var a = ma(sa);
            if (a.State.Error.IsSome) return new IsotopeState<B>(default(B), a.State);

            var b = f(a.Value)(a.State);
            return b;
        };

        public static Isotope<C> SelectMany<A, B, C>(this Isotope<A> ma, Func<A, Isotope<B>> bind, Func<A, B, C> project) => sa =>
        {
            var a = ma(sa);
            if (a.State.Error.IsSome) return new IsotopeState<C>(default(C), a.State);

            var b = bind(a.Value)(a.State);
            if (b.State.Error.IsSome) return new IsotopeState<C>(default(C), b.State);

            return new IsotopeState<C>(project(a.Value, b.Value), b.State);
        };

        public static Isotope<A> ToIsotope<A>(this Option<A> maybe, string label) =>
            maybe.Match(
                    Some: pure,
                    None: () => fail<A>(label));

        public static Isotope<A> ToIsotope<A>(this Try<A> tried, string label) =>
            tried.Match(
                    Succ: pure,
                    Fail: x => fail<A>($"{label} {Environment.NewLine}Details: {x.Message}"));

        public static Isotope<A> ToIsotope<A>(this Try<A> tried, Func<Exception, string> makeError) =>
            tried.Match(
                    Succ: pure,
                    Fail: x => fail<A>(makeError(x)));

        public static Isotope<B> ToIsotope<A, B>(this Either<A, B> either, Func<A, string> makeError) =>
            either.Match(
                Left: l => fail<B>(makeError(l)),
                Right: pure);

        /// <summary>
        /// Finds an element by a selector and checks if it is currently displayed
        /// </summary>
        /// <param name="selector">WebDriver selector</param>
        /// <returns>True if the element is currently displayed</returns>
        public static Isotope<bool> displayed(By selector) =>
            from el in findElement(selector)
            from d in displayed(el)
            select d;

        /// <summary>
        /// Checks if an element is currently displayed
        /// </summary>
        /// <param name="el">WebDriver element</param>
        /// <returns>True if the element is currently displayed</returns>
        public static Isotope<bool> displayed(IWebElement el) =>
            tryf(() => el.Displayed, $"Error getting display status of {el}");

        public static Isotope<bool> enabled(IWebElement el) =>
            tryf(() => el.Enabled, $"Error getting enabled status of {el}");

        /// <summary>
        /// Checks if an element exists that matches the selector
        /// </summary>
        /// <param name="selector">WebDriver selector</param>
        /// <returns>True if a matching element exists</returns>
        public static Isotope<bool> exists(By selector) =>
            from op in findOptionalElement(selector)
            from bl in op.Match(
                        Some: _ => pure(true),
                        None: () => pure(false))
            select bl;

        /// <summary>
        /// Checks whether the centre point of an element is the foremost element at that position on the page.
        /// (Uses the JavaScript document.elementFromPoint function)
        /// </summary>
        /// <param name="element">Target element</param>
        /// <returns>true if the element is foremost</returns>
        public static Isotope<bool> obscured(IWebElement element) =>
            from dvr in webDriver
            let jsExec = (IJavaScriptExecutor)dvr
            let coords = element.Location
            let x = coords.X + (int)Math.Floor((double)(element.Size.Width / 2))
            let y = coords.Y + (int)Math.Floor((double)(element.Size.Height / 2))
            from _ in log($"X: {x}, Y: {y}")
            from top in pure((IWebElement)jsExec.ExecuteScript($"return document.elementFromPoint({x}, {y});"))
            from _1  in log($"Target: {element.PrettyPrint()}, Top: {top.PrettyPrint()}")
            select !element.Equals(top);

        /// <summary>
        /// Compares the text of an element with a string
        /// </summary>
        /// <param name="element">Element to compare</param>
        /// <param name="comparison">String to match</param>
        /// <returns>true if exact match</returns>
        public static Isotope<bool> hasText(IWebElement element, string comparison) =>
            from t in text(element)
            select t == comparison;

        /// <summary>
        /// Repeatedly runs an Isotope function and checks whether the condition is met.
        /// </summary>        
        public static Isotope<A> waitUntil<A>(
            Isotope<A> iso,
            Func<A, bool> continueCondition,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from w in wait.Match(Some: s => pure(s), None: () => defaultWait)
            from i in interval.Match(Some: s => pure(s), None: () => defaultInterval)
            from r in waitUntil(iso, continueCondition, i, w, DateTime.Now)
            select r;

        private static Isotope<A> waitUntil<A>(
            Isotope<A> iso,
            Func<A, bool> continueCondition,
            TimeSpan interval,
            TimeSpan wait,
            DateTime started) =>
            DateTime.Now - started >= wait
                ? fail<A>("Timed Out")
                : from x in iso
                  from y in continueCondition(x)
                            ? from _ in pause(interval)
                              from r in waitUntil(iso, continueCondition, interval, wait, started)
                              select r
                            : pure(x)
                  select y;


        public static Isotope<A> doWhile<A>(
            Isotope<A> iso,
            Func<A, bool> continueCondition,
            int maxRepeats = 100) =>
            maxRepeats <= 0
                ? pure(default(A))
                : from x in iso
                  from y in continueCondition(x)
                              ? doWhile(iso, continueCondition, maxRepeats - 1)
                              : pure(x)
                  select y;

        public static Isotope<A> doWhileOrFail<A>(
            Isotope<A> iso,
            Func<A, bool> continueCondition,
            string failureMessage,
            int maxRepeats = 100) =>
            maxRepeats <= 0
                ? fail<A>(failureMessage)
                : from x in iso
                  from y in continueCondition(x)
                              ? doWhileOrFail(iso, continueCondition, failureMessage, maxRepeats - 1)
                              : pure(x)
                  select y;

        public static Isotope<A> doWhileOrFail<A>(
            Isotope<A> iso,
            Func<A, bool> continueCondition,
            string failureMessage,
            TimeSpan interval,
            int maxRepeats = 1000) =>
            maxRepeats <= 0
                ? fail<A>(failureMessage)
                : from x in iso
                  from y in continueCondition(x)
                              ? from _ in pause(interval)
                                from z in doWhileOrFail(iso, continueCondition, failureMessage, interval, maxRepeats - 1)
                                select z
                              : pure(x)
                  select y;

        /// <summary>
        /// Takes a screenshot if the current WebDriver supports that functionality
        /// </summary>
        public static Isotope<Option<Screenshot>> getScreenshot =>
            from dvr in webDriver
            let ts = dvr as ITakesScreenshot
            select ts == null ? None : Some(ts.GetScreenshot());

    }
}
