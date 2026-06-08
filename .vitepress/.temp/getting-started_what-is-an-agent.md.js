import { ssrRenderAttrs } from "vue/server-renderer";
import { useSSRContext } from "vue";
import { _ as _export_sfc } from "./plugin-vue_export-helper.1tPrXgE0.js";
const __pageData = JSON.parse('{"title":"What Is An Agent?","description":"","frontmatter":{},"headers":[],"relativePath":"getting-started/what-is-an-agent.md","filePath":"getting-started/what-is-an-agent.md","lastUpdated":null}');
const _sfc_main = { name: "getting-started/what-is-an-agent.md" };
function _sfc_ssrRender(_ctx, _push, _parent, _attrs, $props, $setup, $data, $options) {
  _push(`<div${ssrRenderAttrs(_attrs)}><h1 id="what-is-an-agent" tabindex="-1">What Is An Agent? <a class="header-anchor" href="#what-is-an-agent" aria-label="Permalink to &quot;What Is An Agent?&quot;">​</a></h1><p>An agent is a program that uses a model to decide what to say or do next. A normal function follows code you wrote ahead of time. An agent can read a request, call tools when it needs outside information or actions, use the results, and then continue until it has a final answer.</p><p>The smallest HPD agent loop looks like this:</p><div class="language-text vp-adaptive-theme line-numbers-mode"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>user message</span></span>
<span class="line"><span>  -&gt; call the model</span></span>
<span class="line"><span>  -&gt; optionally call tools</span></span>
<span class="line"><span>  -&gt; optionally call the model again with tool results</span></span>
<span class="line"><span>  -&gt; final assistant response</span></span></code></pre><div class="line-numbers-wrapper" aria-hidden="true"><span class="line-number">1</span><br><span class="line-number">2</span><br><span class="line-number">3</span><br><span class="line-number">4</span><br><span class="line-number">5</span><br></div></div><p>HPD Agent gives that loop a .NET shape:</p><div class="language-text vp-adaptive-theme line-numbers-mode"><button title="Copy Code" class="copy"></button><span class="lang">text</span><pre class="shiki shiki-themes github-light github-dark vp-code" tabindex="0"><code><span class="line"><span>AgentBuilder configures the agent</span></span>
<span class="line"><span>Agent runs turns</span></span>
<span class="line"><span>tools expose C# methods</span></span>
<span class="line"><span>sessions and branches hold history</span></span>
<span class="line"><span>events show what is happening while the turn runs</span></span>
<span class="line"><span>middleware adds behavior around turns and tool calls</span></span></code></pre><div class="line-numbers-wrapper" aria-hidden="true"><span class="line-number">1</span><br><span class="line-number">2</span><br><span class="line-number">3</span><br><span class="line-number">4</span><br><span class="line-number">5</span><br><span class="line-number">6</span><br></div></div><h2 id="when-to-use-an-agent" tabindex="-1">When To Use An Agent <a class="header-anchor" href="#when-to-use-an-agent" aria-label="Permalink to &quot;When To Use An Agent&quot;">​</a></h2><p>Use an agent when the useful part is language understanding, generation, planning, or model-directed tool use.</p><p>Use ordinary C# when the task is deterministic. If code can decide exactly what to do every time, keep it as code. If the model needs to interpret the request, choose tools, write text, or adapt to context, use an agent.</p><h2 id="first-30-minutes" tabindex="-1">First 30 Minutes <a class="header-anchor" href="#first-30-minutes" aria-label="Permalink to &quot;First 30 Minutes&quot;">​</a></h2><p>The first path builds one local agent, streams output, gives it one tool, keeps conversation history, and then turns it into a tiny console chat loop.</p><p>Read these next:</p><ol><li><a href="./hello-agent">Hello Agent</a></li><li><a href="./streaming-events">Streaming Events</a></li><li><a href="./add-a-tool">Add A Tool</a></li><li><a href="./multi-turn-sessions">Multi-Turn Sessions</a></li><li><a href="./chat-loop">Tiny Console Chat Loop</a></li></ol></div>`);
}
const _sfc_setup = _sfc_main.setup;
_sfc_main.setup = (props, ctx) => {
  const ssrContext = useSSRContext();
  (ssrContext.modules || (ssrContext.modules = /* @__PURE__ */ new Set())).add("getting-started/what-is-an-agent.md");
  return _sfc_setup ? _sfc_setup(props, ctx) : void 0;
};
const whatIsAnAgent = /* @__PURE__ */ _export_sfc(_sfc_main, [["ssrRender", _sfc_ssrRender]]);
export {
  __pageData,
  whatIsAnAgent as default
};
