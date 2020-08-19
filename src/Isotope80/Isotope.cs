﻿using LanguageExt;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using static LanguageExt.Prelude;

namespace Isotope80
{
    public delegate IsotopeState<A> Isotope<Env, A>(Env env, IsotopeState state);

    public static partial class Isotope
    {
        /// <summary>
        /// Run the test computation - returning an optional error. 
        /// The computation succeeds if result.IsNone is true
        /// </summary>
        /// <param name="ma">Test computation</param>
        public static (IsotopeState state, A value) Run<Env, A>(this Isotope<Env, A> ma, Env env, IsotopeSettings settings = null)
        {
            var res = ma(env, IsotopeState.Empty.With(Settings: settings));

            if (res.State.Settings.DisposeOnCompletion)
            {
                res.State.DisposeWebDriver();
            }

            return(res.State, res.Value);
        }

        public static (IsotopeState state, A value) Run<Env, A>(this Isotope<Env, A> ma, Env env, IWebDriver driver, IsotopeSettings settings = null)
        {
            var res = ma(env, IsotopeState.Empty.With(Driver: Some(driver), Settings: settings));

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
        public static (IsotopeState state, A value) RunAndThrowOnError<Env, A>(this Isotope<Env, A> ma, Env env, IWebDriver driver, IsotopeSettings settings = null)
        {
            var res = ma(env, IsotopeState.Empty.With(Driver: Some(driver), Settings: settings));

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
        public static Isotope<Env, Unit> initConfig<Env>(params (string, string)[] config) =>
            initConfig<Env>(toMap(config));

        /// <summary>
        /// Simple configuration setup
        /// </summary>
        /// <param name="config">Map of config items</param>
        public static Isotope<Env, Unit> initConfig<Env>(Map<string, string> config) =>
            from s in get<Env>()
            from _ in put<Env>(s.With(Configuration: config))
            select unit;

        /// <summary>
        /// Get a config key
        /// </summary>
        /// <param name="key"></param>
        public static Isotope<Env, string> config<Env>(string key) =>
            from s in get<Env>()
            from r in s.Configuration.Find(key).ToIsotope<Env, string>($"Configuration key not found: {key}")
            select r;

        public static Isotope<Env, Unit> initSettings<Env>(IsotopeSettings settings) =>
            from s in get<Env>()
            from _ in put<Env>(s.With(Settings: settings))
            select unit;

        public static Isotope<Env, Unit> setWindowSize<Env>(int width, int height) =>
            from _ in setWindowSize<Env>(new Size(width, height))
            select unit;

        public static Isotope<Env, Unit> setWindowSize<Env>(Size size) =>
            from d in webDriver<Env>()
            from _ in trya<Env>(() => d.Manage().Window.Size = size, $"Failed to change browser window size to {size}")
            select unit;

        /// <summary>
        /// Navigate to a URL
        /// </summary>
        /// <param name="url">URL to navigate to</param>
        public static Isotope<Env, Unit> nav<Env>(string url) =>
            from d in webDriver<Env>()
            from _ in trya<Env>(() => d.Navigate().GoToUrl(url), $"Failed to navigate to: {url}")
            select unit;

        /// <summary>
        /// Gets the URL currently displayed by the browser
        /// </summary>
        public static Isotope<Env, string> url<Env>() =>
            from d in webDriver<Env>()
            select d.Url;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">CSS selector</param>
        public static Isotope<Env, IWebElement> findElement<Env>(By selector, bool wait = true, string errorMessage = "Unable to find element") =>
            from d in webDriver<Env>()
            from e in wait ? waitUntilElementExists<Env>(selector) : fail<Env, IWebElement>(errorMessage)
            select e;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">Selector</param>
        public static Isotope<Env, Option<IWebElement>> findOptionalElement<Env>(IWebElement element, By selector, string errorMessage = null) =>
            from es in findElementsOrEmpty<Env>(element, selector, errorMessage)
            from e in pure<Env, Option<IWebElement>>(es.HeadOrNone())
            select e;

        public static Isotope<Env, Option<IWebElement>> findOptionalElement<Env>(By selector, string errorMessage = null) =>
            from es in findElementsOrEmpty<Env>(selector, errorMessage)
            from e in pure<Env, Option<IWebElement>>(es.HeadOrNone())
            select e;

        /// <summary>
        /// Find an HTML element
        /// </summary>
        /// <param name="selector">CSS selector</param>
        public static Isotope<Env, IWebElement> findElement<Env>(
            IWebElement element, 
            By selector, 
            bool wait = true, 
            string errorMessage = null) =>
            from d in webDriver<Env>()
            from e in wait 
                      ? waitUntilElementExists<Env>(element, selector)
                      : findChildElement<Env>(element, selector) 
            select e;

        private static Isotope<Env, IWebElement> findChildElement<Env>(
            IWebElement parent,
            By selector,
            string errorMessage = null) =>
            tryf<Env, IWebElement>(() => parent.FindElement(selector),
                 errorMessage ?? $"Can't find element {selector}");

        /// <summary>
        /// Find HTML elements
        /// </summary>
        /// <param name="selector">Selector</param>
        public static Isotope<Env, Seq<IWebElement>> findElements<Env>(By selector, bool wait = true, string error = null) =>
            wait ? waitUntilElementsExists<Env>(selector)
                 : from d in webDriver<Env>()
                   from es in tryf<Env, Seq<IWebElement>>(() => d.FindElements(selector).ToSeq(),
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
        public static Isotope<Env, Seq<IWebElement>> findElements<Env>(IWebElement parent, By selector, bool wait = true, string error = null) =>
            wait ? waitUntilElementsExists<Env>(parent, selector)
                 : Try(() => parent.FindElements(selector).ToSeq()).
                    Match(
                     Succ: x => x.IsEmpty ? fail<Env, Seq<IWebElement>>(error ?? $"Can't find any elements {selector}")
                                          : pure<Env, Seq<IWebElement>>(x),
                     Fail: e => fail<Env, Seq<IWebElement>>(error ?? $"Can't find any elements {selector}"));

        /// <summary>
        /// Find a sequence of elements matching a selector
        /// </summary>
        /// <param name="selector">Web Driver selector</param>
        /// <param name="error"></param>
        /// <returns>Sequence of matching elements</returns>
        public static Isotope<Env, Seq<IWebElement>> findElementsOrEmpty<Env>(By selector, string error = null) =>
            from d in webDriver<Env>()
            from e in tryf<Env, Seq<IWebElement>>(() => d.FindElements(selector).ToSeq(), error ?? $"Can't find any elements {selector}")
            select e;

        /// <summary>
        /// Find a sequence of elements within an existing element matching a selector
        /// </summary>
        /// <param name="parent">Parent element</param>
        /// <param name="selector">Web Driver selector</param>
        /// <param name="error"></param>
        /// <returns>Sequence of matching elements</returns>
        public static Isotope<Env, Seq<IWebElement>> findElementsOrEmpty<Env>(IWebElement parent, By selector, string error = null) =>
            from e in tryf<Env, Seq<IWebElement>>(() => parent.FindElements(selector).ToSeq(), error ?? $"Can't find any elements {selector}")
            select e;

        /// <summary>
        /// Find a &lt;select&gt; element within an existing element
        /// </summary>  
        public static Isotope<Env, SelectElement> findSelectElement<Env>(IWebElement container, By selector) =>
            from el in findElement<Env>(container, selector)
            from se in toSelectElement<Env>(el)
            select se;

        /// <summary>
        /// Find a &lt;select&gt; element
        /// </summary>        
        public static Isotope<Env, SelectElement> findSelectElement<Env>(By selector) =>
            from el in findElement<Env>(selector)
            from se in toSelectElement<Env>(el)
            select se;

        /// <summary>
        /// Convert an IWebElement to a SelectElement
        /// </summary>  
        public static Isotope<Env, SelectElement> toSelectElement<Env>(IWebElement element) =>
            tryf<Env, SelectElement>(() => new SelectElement(element), x => "Problem creating select element: " + x.Message);

        /// <summary>
        /// Select a &lt;select&gt; option by text
        /// </summary>     
        public static Isotope<Env, Unit> selectByText<Env>(By selector, string text) =>
            from se in findSelectElement<Env>(selector)
            from _  in selectByText<Env>(se, text)
            select unit;

        /// <summary>
        /// Select a &lt;select&gt; option by text
        /// </summary>        
        public static Isotope<Env, Unit> selectByText<Env>(SelectElement select, string text) =>
            trya<Env>(() => select.SelectByText(text), x => "Unable to select" + x.Message);

        /// <summary>
        /// Select a &lt;select&gt; option by value
        /// </summary>     
        public static Isotope<Env, Unit> selectByValue<Env>(By selector, string value) =>
            from se in findSelectElement<Env>(selector)
            from _  in selectByValue<Env>(se, value)
            select unit;

        /// <summary>
        /// Select a &lt;select&gt; option by value
        /// </summary>        
        public static Isotope<Env, Unit> selectByValue<Env>(SelectElement select, string value) =>
            trya<Env>(() => select.SelectByValue(value), x => "Unable to select" + x.Message);

        /// <summary>
        /// Retrieves the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<Env, IWebElement> getSelectedOption<Env>(SelectElement sel) =>
            tryf<Env, IWebElement>(() => sel.SelectedOption, x => "Unable to get selected option" + x.Message);

        /// <summary>
        /// Retrieves the text for the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<Env, string> getSelectedOptionText<Env>(SelectElement sel) =>
            from opt in getSelectedOption<Env>(sel)
            from txt in text<Env>(opt)
            select txt;

        /// <summary>
        /// Retrieves the value for the selected option element in a Select Element
        /// </summary>
        /// <param name="sel">Web Driver Select Element</param>
        /// <returns>The selected Option Web Element</returns>
        public static Isotope<Env, string> getSelectedOptionValue<Env>(SelectElement sel) =>
            from opt in getSelectedOption<Env>(sel)
            from val in value<Env>(opt)
            select val;

        /// <summary>
        /// Finds a checkbox element by selector and identifies whether it is checked
        /// </summary>
        /// <param name="selector">Web Driver Selector</param>
        /// <returns>Is checked\s</returns>
        public static Isotope<Env, bool> isCheckboxChecked<Env>(By selector) =>
            from el in findElement<Env>(selector)
            from res in isCheckboxChecked<Env>(el)
            select res;

        /// <summary>
        /// Identifies whether an existing checkbox is checked
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <returns>Is checked\s</returns>
        public static Isotope<Env, bool> isCheckboxChecked<Env>(IWebElement el) =>
            pure<Env, bool>(el.Selected);

        /// <summary>
        /// Set checkbox value for existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="ticked">Check the box or not</param>
        /// <returns>Unit</returns>
        public static Isotope<Env, Unit> setCheckbox<Env>(IWebElement el, bool ticked) =>
            from val in isCheckboxChecked<Env>(el)
            from _   in val == ticked
                        ? pure<Env, Unit>(unit)
                        : click<Env>(el)
            select unit;

        /// <summary>
        /// Looks for a particular style attribute on an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="style">Style attribute to look up</param>
        /// <returns>A string representing the style value</returns>
        public static Isotope<Env, string> getStyle<Env>(IWebElement el, string style) =>
            tryf<Env, string>(() => el.GetCssValue(style), $"Could not find style {style}");

        /// <summary>
        /// Gets the Z Index style attribute value for an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <returns>The Z Index value</returns>
        public static Isotope<Env, int> getZIndex<Env>(IWebElement el) =>
            from zis in getStyle<Env>(el, "zIndex")
            from zii in parseInt(zis).ToIsotope<Env, int>($"z-Index was not valid integer: {zis}.")
            select zii;

        /// <summary>
        /// Looks for a particular style attribute on an existing element
        /// </summary>
        /// <param name="el">Web Driver Element</param>
        /// <param name="att">Attribute to look up</param>
        /// <returns>A string representing the attribute value</returns>
        public static Isotope<Env, string> attribute<Env>(IWebElement el, string att) =>
            tryf<Env, string>(() => el.GetAttribute(att), $"Attribute {att} could not be found.");

        /// <summary>
        /// Simulates keyboard by sending `keys` 
        /// </summary>
        /// <param name="selector">Selector for element to type into</param>
        /// <param name="keys">String of characters that are typed</param>
        public static Isotope<Env, Unit> sendKeys<Env>(By selector, string keys) =>
            from el in findElement<Env>(selector)
            from _  in sendKeys<Env>(el, keys)
            select unit;

        /// <summary>
        /// Simulates keyboard by sending `keys` 
        /// </summary>
        /// <param name="element">Element to type into</param>
        /// <param name="keys">String of characters that are typed</param>
        public static Isotope<Env, Unit> sendKeys<Env>(IWebElement element, string keys) =>
            trya<Env>(() => element.SendKeys(keys), $@"Error sending keys ""{keys}"" to element: {element.PrettyPrint()}");

        /// <summary>
        /// Simulates the mouse-click
        /// </summary>
        /// <param name="selector">Web Driver Selector</param>
        /// <returns>Unit</returns>
        public static Isotope<Env, Unit> click<Env>(By selector) =>
            from el in findElement<Env>(selector)
            from _ in click<Env>(el)
            select unit;

        /// <summary>
        /// Simulates the mouse-click
        /// </summary>
        /// <param name="element">Element to click</param>
        public static Isotope<Env, Unit> click<Env>(IWebElement element) =>
            trya<Env>(() => element.Click(), $@"Error clicking element: {element.PrettyPrint()}");

        /// <summary>
        /// Clears the content of an element
        /// </summary>
        /// <param name="element">Web Driver Element</param>
        /// <returns>Unit</returns>
        public static Isotope<Env, Unit> clear<Env>(IWebElement element) =>
            trya<Env>(() => element.Clear(), $@"Error clearing element: {element.PrettyPrint()}");

        /// <summary>
        /// ONLY USE AS A LAST RESORT
        /// Pauses the processing for an interval to brute force waiting for actions to complete
        /// </summary>
        public static Isotope<Env, Unit> pause<Env>(TimeSpan interval)
        {
            Thread.Sleep((int)interval.TotalMilliseconds);
            return pure<Env, Unit>(unit);
        }

        /// <summary>
        /// Gets the text inside an element
        /// </summary>
        /// <param name="element">Element containing txt</param>
        public static Isotope<Env, string> text<Env>(IWebElement element) =>
            tryf<Env, string>(() => element.Text, $@"Error getting text from element: {element.PrettyPrint()}");

        /// <summary>
        /// Gets the value attribute of an element
        /// </summary>
        /// <param name="element">Element containing value</param>
        public static Isotope<Env, string> value<Env>(IWebElement element) =>
            tryf<Env, string>(() => element.GetAttribute("Value"), $@"Error getting value from element: {element.PrettyPrint()}");

        /// <summary>
        /// Web driver accessor
        /// </summary>
        public static Isotope<Env, IWebDriver> webDriver<Env>() =>
            from s in get<Env>()
            from r in s.Driver.ToIsotope<Env, IWebDriver>("web-driver hasn't been selected yet")
            select r;

        /// <summary>
        /// Web driver setter
        /// </summary>
        public static Isotope<Env, Unit> setWebDriver<Env>(IWebDriver d) =>
            from s in get<Env>()
            from _ in put<Env>(s.With(Driver: Some(d)))
            select unit;

        public static Isotope<Env, Unit> disposeWebDriver<Env>() =>
            from s in get<Env>()
            select s.DisposeWebDriver();

        /// <summary>
        /// Default wait accessor
        /// </summary>
        public static Isotope<Env, TimeSpan> defaultWait<Env>() =>
            from s in get<Env>()
            select s.Settings.Wait;

        /// <summary>
        /// Default wait accessor
        /// </summary>
        public static Isotope<Env, TimeSpan> defaultInterval<Env>() =>
            from s in get<Env>()
            select s.Settings.Interval;

        /// <summary>
        /// Identity - lifts a value of `A` into the Isotope monad
        /// 
        /// * Always succeeds *
        /// 
        /// </summary>
        public static Isotope<Env, A> pure<Env, A>(A value) =>
            (env, state) =>
                new IsotopeState<A>(value, state);

        /// <summary>
        /// Useful for starting a linq expression if you need lets first
        /// i.e.
        ///         from _ in unitM
        ///         let foo = "123"
        ///         let bar = "456"
        ///         from x in ....
        /// </summary>
        public static Isotope<Env, Unit> unitM<Env>() => pure<Env, Unit>(unit);

        /// <summary>
        /// Failure - creates an Isotope monad that always fails
        /// </summary>
        /// <param name="message">Error message</param>
        public static Isotope<Env, A> fail<Env, A>(string message) =>
            (env, state) =>
                new IsotopeState<A>(default, state.With(Error: Some(message)));

        /// <summary>
        /// Gets the environment from the Isotope monad
        /// </summary>
        /// <typeparam name="Env">Environment</typeparam>
        public static Isotope<Env, Env> ask<Env>() =>
            (env, state) =>
                new IsotopeState<Env>(env, state);

        /// <summary>
        /// Gets a function of the current environment
        /// </summary>
        public static Isotope<Env, R> asks<Env, R>(Func<Env, R> f) =>
            from env in ask<Env>()
            select f(env);

        /// <summary>
        /// Gets the state from the Isotope monad
        /// </summary>
        public static Isotope<Env, IsotopeState> get<Env>() =>
            (env, state) =>
                new IsotopeState<IsotopeState>(state, state);

        /// <summary>
        /// Puts the state back into the Isotope monad
        /// </summary>
        public static Isotope<Env, Unit> put<Env>(IsotopeState state) =>
            (_, env) =>
                new IsotopeState<Unit>(unit, state);

        /// <summary>
        /// Try and action
        /// </summary>
        /// <param name="action">Action to try</param>
        /// <param name="label">Error string if exception is thrown</param>
        public static Isotope<Env, Unit> trya<Env>(Action action, string label) =>
            Try(() => { action(); return unit; }).ToIsotope<Env, Unit>(label);

        /// <summary>
        /// Try an action
        /// </summary>
        /// <param name="action">Action to try</param>
        /// <param name="makeError">Convert Exception to an error string</param>
        public static Isotope<Env, Unit> trya<Env>(Action action, Func<Exception, string> makeError) =>
            Try(() => { action(); return unit; }).ToIsotope<Env, Unit>(makeError);

        /// <summary>
        /// Try a function
        /// </summary>
        /// <typeparam name="A">Return type of the function</typeparam>
        /// <param name="func">Function to try</param>
        /// <param name="label">Error string if exception is thrown</param>
        public static Isotope<Env, A> tryf<Env, A>(Func<A> func, string label) =>
            Try(() => func()).ToIsotope<Env, A>(label);

        /// <summary>
        /// Try a function
        /// </summary>
        /// <typeparam name="A">Return type of the function</typeparam>
        /// <param name="func">Function to try</param>
        /// <param name="makeError">Convert Exception to an error string</param>
        /// <returns>The result of the function</returns>
        public static Isotope<Env, A> tryf<Env, A>(Func<A> func, Func<Exception, string> makeError) =>
            Try(() => func()).ToIsotope<Env, A>(makeError);

        /// <summary>
        /// Run a void returning action
        /// </summary>
        /// <param name="action">Action to run</param>
        /// <returns>Unit</returns>
        public static Isotope<Env, Unit> voida<Env>(Action action) => (env, state) =>
        {
            action();
            return new IsotopeState<Unit>(unit, state);
        };

        /// <summary>
        /// Log some output
        /// </summary>
        public static Isotope<Env, Unit> log<Env>(string message) =>
            from st in get<Env>()
            from _1 in put<Env>(st.Write(message, st.Settings.LoggingAction))
            select unit;

        public static Isotope<Env, Unit> pushLog<Env>(string message) =>
            from st in get<Env>()
            from _1 in put<Env>(st.PushLog(message, st.Settings.LoggingAction))
            select unit;

        public static Isotope<Env, Unit> popLog<Env>() =>
            from st in get<Env>()
            from _1 in put<Env>(st.PopLog())
            select unit;

        public static Isotope<Env, A> context<Env, A>(string context, Isotope<Env, A> iso) =>
            from _1 in pushLog<Env>(context)
            from re in iso
            from _2 in popLog<Env>()
            select re;

        public static Isotope<Env, Seq<IWebElement>> waitUntilElementsExists<Env>(
            By selector,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from el in waitUntil(findElementsOrEmpty<Env>(selector), x => x.IsEmpty, interval: interval, wait: wait)
            select el;

        public static Isotope<Env, Seq<IWebElement>> waitUntilElementsExists<Env>(
            IWebElement parent,
            By selector,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from el in waitUntil(findElementsOrEmpty<Env>(parent, selector), x => x.IsEmpty, interval: interval, wait: wait)
            select el;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selector"></param>
        /// <param name="interval"></param>
        /// <param name="wait"></param>
        /// <returns></returns>
        public static Isotope<Env, IWebElement> waitUntilElementExists<Env>(
            By selector, 
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from x in waitUntil(
                            findOptionalElement<Env>(selector),
                            el => el.IsNone,
                            interval,
                            wait)
            from y in x.Match(
                            Some: s => pure<Env, IWebElement>(s),
                            None: () => fail<Env, IWebElement>("Element not found within timeout period"))
            select y;

        /// <summary>
        /// Attempts to find a child element within an existing element and if not present retries for a period.
        /// </summary>
        /// <param name="element">Parent element</param>
        /// <param name="selector">Selector within element</param>
        /// <param name="interval">The time period between attempts to check, if not provided the default value from Settings is used.</param>
        /// <param name="wait">The overall time period to attempt for, if not provided the default value from Settings is used.</param>
        /// <returns></returns>
        public static Isotope<Env, IWebElement> waitUntilElementExists<Env>(
            IWebElement element, 
            By selector, 
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from x in waitUntil(
                            findOptionalElement<Env>(element, selector),
                            el => el.IsNone,
                            interval,
                            wait)
            from y in x.Match(
                            Some: s => pure<Env, IWebElement>(s),
                            None: () => fail<Env, IWebElement>("Element not found within timeout period"))
            select y;

        /// <summary>
        /// Wait for an element to be rendered and clickable, fail if exceeds default timeout
        /// </summary>
        public static Isotope<Env, IWebElement> waitUntilClickable<Env>(By selector) =>
            from w  in defaultWait<Env>()
            from el in waitUntilClickable<Env>(selector, w)
            select el;

        /// <summary>
        /// Wait for an element to be rendered and clickable, fail if exceeds default timeout
        /// </summary>
        public static Isotope<Env, Unit> waitUntilClickable<Env>(IWebElement element) =>
            from w in defaultWait<Env>()
            from _ in waitUntilClickable<Env>(element, w)
            select unit;

        public static Isotope<Env, IWebElement> waitUntilClickable<Env>(By selector, TimeSpan timeout) =>
            from _1 in log<Env>($"Waiting until clickable: {selector}")
            from el in waitUntilElementExists<Env>(selector)
            from _2 in waitUntilClickable<Env>(el, timeout)
            select el;

        public static Isotope<Env, Unit> waitUntilClickable<Env>(IWebElement el, TimeSpan timeout) =>
            from _ in waitUntil(
                        from _1a in log<Env>($"Checking clickability " + el.PrettyPrint())
                        from d in displayed<Env>(el)
                        from e in enabled<Env>(el)
                        from o in obscured<Env>(el)
                        from _2a in log<Env>($"Displayed: {d}, Enabled: {e}, Obscured: {o}")
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
        public static Isotope<Env, Seq<A>> Sequence<Env, A>(this Seq<Isotope<Env, A>> mas) =>
            (env, state) =>
            {
                var rs = new A[mas.Count];
                int index = 0;

                foreach (var ma in mas)
                {
                    var s = ma(env, state);
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
        public static Isotope<Env, Seq<A>> Collect<Env, A>(this Seq<Isotope<Env, A>> mas) =>
            (env, state) =>
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
                    var s = ma(env, state);

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

        public static Isotope<Env, B> Select<Env, A, B>(this Isotope<Env, A> ma, Func<A, B> f) =>
            (env, sa) =>
            {
                var a = ma(env, sa);
                if (a.State.Error.IsSome) return new IsotopeState<B>(default(B), a.State);
                return new IsotopeState<B>(f(a.Value), a.State);
            };

        public static Isotope<Env, B> Map<Env, A, B>(this Isotope<Env, A> ma, Func<A, B> f) => ma.Select(f);

        public static Isotope<Env, B> Bind<Env, A, B>(this Isotope<Env, A> ma, Func<A, Isotope<Env, B>> f) => SelectMany(ma, f);

        public static Isotope<Env, B> SelectMany<Env, A, B>(this Isotope<Env, A> ma, Func<A, Isotope<Env, B>> f) =>
            (env, sa) =>
            {
                if (sa.Error.IsSome) return new IsotopeState<B>(default, sa);

                var a = ma(env, sa);
                if (a.State.Error.IsSome) return new IsotopeState<B>(default(B), a.State);

                var b = f(a.Value)(env, a.State);
                return b;
            };

        public static Isotope<Env, C> SelectMany<Env, A, B, C>(this Isotope<Env, A> ma, Func<A, Isotope<Env, B>> bind, Func<A, B, C> project) =>
            (env, sa) =>
            {
                var a = ma(env, sa);
                if (a.State.Error.IsSome) return new IsotopeState<C>(default(C), a.State);

                var b = bind(a.Value)(env, a.State);
                if (b.State.Error.IsSome) return new IsotopeState<C>(default(C), b.State);

                return new IsotopeState<C>(project(a.Value, b.Value), b.State);
            };

        public static Isotope<Env, A> ToIsotope<Env, A>(this Option<A> maybe, string label) =>
            maybe.Match(
                    Some: pure<Env, A>,
                    None: () => fail<Env, A>(label));

        public static Isotope<Env, A> ToIsotope<Env, A>(this Try<A> tried, string label) =>
            tried.Match(
                    Succ: pure<Env, A>,
                    Fail: x => fail<Env, A>($"{label} {Environment.NewLine}Details: {x.Message}"));

        public static Isotope<Env, A> ToIsotope<Env, A>(this Try<A> tried, Func<Exception, string> makeError) =>
            tried.Match(
                    Succ: pure<Env, A>,
                    Fail: x => fail<Env, A>(makeError(x)));

        public static Isotope<Env, B> ToIsotope<Env, A, B>(this Either<A, B> either, Func<A, string> makeError) =>
            either.Match(
                Left: l => fail<Env, B>(makeError(l)),
                Right: pure<Env, B>);

        /// <summary>
        /// Finds an element by a selector and checks if it is currently displayed
        /// </summary>
        /// <param name="selector">WebDriver selector</param>
        /// <returns>True if the element is currently displayed</returns>
        public static Isotope<Env, bool> displayed<Env>(By selector) =>
            from el in findElement<Env>(selector)
            from d in displayed<Env>(el)
            select d;

        /// <summary>
        /// Checks if an element is currently displayed
        /// </summary>
        /// <param name="el">WebDriver element</param>
        /// <returns>True if the element is currently displayed</returns>
        public static Isotope<Env, bool> displayed<Env>(IWebElement el) =>
            tryf<Env, bool>(() => el.Displayed, $"Error getting display status of {el}");

        public static Isotope<Env, bool> enabled<Env>(IWebElement el) =>
            tryf<Env, bool>(() => el.Enabled, $"Error getting enabled status of {el}");

        /// <summary>
        /// Checks if an element exists that matches the selector
        /// </summary>
        /// <param name="selector">WebDriver selector</param>
        /// <returns>True if a matching element exists</returns>
        public static Isotope<Env, bool> exists<Env>(By selector) =>
            from op in findOptionalElement<Env>(selector)
            from bl in op.Match(
                        Some: _ => pure<Env, bool>(true),
                        None: () => pure<Env, bool>(false))
            select bl;

        /// <summary>
        /// Checks whether the centre point of an element is the foremost element at that position on the page.
        /// (Uses the JavaScript document.elementFromPoint function)
        /// </summary>
        /// <param name="element">Target element</param>
        /// <returns>true if the element is foremost</returns>
        public static Isotope<Env, bool> obscured<Env>(IWebElement element) =>
            from dvr in webDriver<Env>()
            let jsExec = (IJavaScriptExecutor)dvr
            let coords = element.Location
            let x = coords.X + (int)Math.Floor((double)(element.Size.Width / 2))
            let y = coords.Y + (int)Math.Floor((double)(element.Size.Height / 2))
            from _ in log<Env>($"X: {x}, Y: {y}")
            from top in pure<Env, IWebElement>((IWebElement)jsExec.ExecuteScript($"return document.elementFromPoint({x}, {y});"))
            from _1  in log<Env>($"Target: {element.PrettyPrint()}, Top: {top.PrettyPrint()}")
            select !element.Equals(top);

        /// <summary>
        /// Compares the text of an element with a string
        /// </summary>
        /// <param name="element">Element to compare</param>
        /// <param name="comparison">String to match</param>
        /// <returns>true if exact match</returns>
        public static Isotope<Env, bool> hasText<Env>(IWebElement element, string comparison) =>
            from t in text<Env>(element)
            select t == comparison;

        /// <summary>
        /// Repeatedly runs an Isotope function and checks whether the condition is met.
        /// </summary>        
        public static Isotope<Env, A> waitUntil<Env, A>(
            Isotope<Env, A> iso,
            Func<A, bool> continueCondition,
            Option<TimeSpan> interval = default,
            Option<TimeSpan> wait = default) =>
            from w in wait.Match(Some: s => pure<Env, TimeSpan>(s), None: () => defaultWait<Env>())
            from i in interval.Match(Some: s => pure<Env, TimeSpan>(s), None: () => defaultInterval<Env>())
            from r in waitUntil(iso, continueCondition, i, w, DateTime.Now)
            select r;

        private static Isotope<Env, A> waitUntil<Env, A>(
            Isotope<Env, A> iso,
            Func<A, bool> continueCondition,
            TimeSpan interval,
            TimeSpan wait,
            DateTime started) =>
            DateTime.Now - started >= wait
                ? fail<Env, A>("Timed Out")
                : from x in iso
                  from y in continueCondition(x)
                            ? from _ in pause<Env>(interval)
                              from r in waitUntil(iso, continueCondition, interval, wait, started)
                              select r
                            : pure<Env, A>(x)
                  select y;


        public static Isotope<Env, A> doWhile<Env, A>(
            Isotope<Env, A> iso,
            Func<A, bool> continueCondition,
            int maxRepeats = 100) =>
            maxRepeats <= 0
                ? pure<Env, A>(default(A))
                : from x in iso
                  from y in continueCondition(x)
                              ? doWhile(iso, continueCondition, maxRepeats - 1)
                              : pure<Env, A>(x)
                  select y;

        public static Isotope<Env, A> doWhileOrFail<Env, A>(
            Isotope<Env, A> iso,
            Func<A, bool> continueCondition,
            string failureMessage,
            int maxRepeats = 100) =>
            maxRepeats <= 0
                ? fail<Env, A>(failureMessage)
                : from x in iso
                  from y in continueCondition(x)
                              ? doWhileOrFail(iso, continueCondition, failureMessage, maxRepeats - 1)
                              : pure<Env, A>(x)
                  select y;

        public static Isotope<Env, A> doWhileOrFail<Env, A>(
            Isotope<Env, A> iso,
            Func<A, bool> continueCondition,
            string failureMessage,
            TimeSpan interval,
            int maxRepeats = 1000) =>
            maxRepeats <= 0
                ? fail<Env, A>(failureMessage)
                : from x in iso
                  from y in continueCondition(x)
                              ? from _ in pause<Env>(interval)
                                from z in doWhileOrFail(iso, continueCondition, failureMessage, interval, maxRepeats - 1)
                                select z
                              : pure<Env, A>(x)
                  select y;

        /// <summary>
        /// Takes a screenshot if the current WebDriver supports that functionality
        /// </summary>
        public static Isotope<Env, Option<Screenshot>> getScreenshot<Env>() =>
            from dvr in webDriver<Env>()
            let ts = dvr as ITakesScreenshot
            select ts == null ? None : Some(ts.GetScreenshot());

    }
}
