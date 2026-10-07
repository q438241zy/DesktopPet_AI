(function(root,factory){const value=factory();if(typeof module==='object'&&module.exports)module.exports=value;else root.CompanionProviders=value;})(globalThis,function(){
  'use strict';
  // Official protocol references and verification limits: docs/companion-api-providers.md.
  const catalog={
    openai:{name:'GPT · OpenAI',protocol:'chat',base:'https://api.openai.com/v1',hint:'gpt-6-sol',doc:'https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create'},
    claude:{name:'Claude · Anthropic',protocol:'messages',base:'https://api.anthropic.com/v1',hint:'claude-opus-5',doc:'https://platform.claude.com/docs/en/api/messages/create'},
    gemini:{name:'Gemini · Google',protocol:'gemini',base:'https://generativelanguage.googleapis.com/v1beta',hint:'gemini-3.8-flash',doc:'https://ai.google.dev/api/generate-content'},
    grok:{name:'Grok · xAI',protocol:'chat',base:'https://api.x.ai/v1',hint:'grok-4.7',doc:'https://docs.x.ai/developers/model-capabilities/legacy/chat-completions'},
    deepseek:{name:'DeepSeek',protocol:'chat',base:'https://api.deepseek.com',hint:'deepseek-flash',doc:'https://api-docs.deepseek.com/'},
    qwen:{name:'Qwen · 阿里云百炼',protocol:'chat',base:'https://dashscope.aliyuncs.com/compatible-mode/v1',hint:'qwen-plus',note:'API Key 与地址需属于同一地域，也可填写业务空间专属地址。',doc:'https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope'},
    glm:{name:'GLM · 智谱',protocol:'chat',base:'https://open.bigmodel.cn/api/paas/v4',hint:'glm-5.3',doc:'https://docs.bigmodel.cn/api-reference/模型-api/对话补全'},
    kimi:{name:'Kimi · Moonshot',protocol:'chat',base:'https://api.moonshot.ai/v1',hint:'kimi-k3',note:'请使用 API Key 所属平台提供的服务地址。',doc:'https://platform.kimi.ai/docs/overview'}
  };
  const local=()=>({provider:'local',endpoint:'',model:'',workspace:''});
  function cleanConfig(value){
    if(!value||!Object.hasOwn(catalog,value.provider))return local();
    return {provider:value.provider,endpoint:String(value.endpoint||'').slice(0,500),model:String(value.model||'').slice(0,120),workspace:String(value.workspace||'').slice(0,100)};
  }
  function safeURL(value){
    let u;try{u=new URL(value.trim());}catch{throw new Error('请输入完整的 API 地址。');}
    const loopback=['localhost','127.0.0.1','[::1]'].includes(u.hostname);
    if((u.protocol!=='https:'&&!(u.protocol==='http:'&&loopback))||u.username||u.password||u.search||u.hash)throw new Error('API 地址需使用 HTTPS；本机地址可使用 HTTP。');
    if(/[{}]/.test(value))throw new Error('请把地址中的业务空间占位符替换为真实 ID。');
    return u;
  }
  function endpoint(value,provider='openai',model=''){
    if(!value.trim())return '';
    const spec=catalog[provider];if(!spec)throw new Error('请选择支持的模型接口。');
    const u=safeURL(value);let path=u.pathname.replace(/\/+$/,'');
    if(spec.protocol==='gemini'){
      if(/\/(chat\/completions|messages|responses)$/.test(path))throw new Error('Gemini 使用原生 generateContent 地址。');
      const full=path.match(/^(.*\/models\/)([^/]+):generateContent$/);
      const name=model.trim().replace(/^models\//,'');
      if(full){
        if(name&&decodeURIComponent(full[2])!==name)throw new Error('Gemini 地址中的模型与模型名称不一致。');
      }else{
        if(!name||!/^[A-Za-z0-9._-]+$/.test(name))throw new Error('请填写有效的 Gemini 模型名称。');
        if(/:|\/models\/.+/.test(path))throw new Error('请填写 Gemini 基础地址或完整 generateContent 地址。');
        path=(path||'/v1beta').replace(/\/models$/,'')+'/models/'+encodeURIComponent(name)+':generateContent';
      }
    }else if(spec.protocol==='messages'){
      if(/\/(chat\/completions|responses)$/.test(path))throw new Error('Claude 使用原生 /v1/messages 接口。');
      if(!path)path='/v1';
      if(!path.endsWith('/messages'))path+='/messages';
    }else{
      if(/\/(responses|messages)$/.test(path)||/:generateContent$/.test(path))throw new Error('此服务使用 Chat Completions，请填写基础地址或 /chat/completions。');
      if(!path)path=new URL(spec.base).pathname.replace(/\/+$/,'');
      if(!path.endsWith('/chat/completions'))path+='/chat/completions';
    }
    u.pathname=path;return u.href;
  }
  function conversation(messages){
    const result=[];
    for(const m of messages.filter(m=>['user','assistant'].includes(m.role)&&typeof m.content==='string'&&m.content.trim()).slice(-24)){
      if(!result.length&&m.role!=='user')continue;
      const previous=result.at(-1);
      if(previous?.role===m.role)previous.content+='\n'+m.content;
      else result.push({role:m.role,content:m.content});
    }
    return result;
  }
  function request(api,key,messages,system){
    if(api.provider==='local'||!api.endpoint.trim())return null;
    const spec=catalog[api.provider];if(!spec)throw new Error('请选择支持的模型接口。');
    const model=api.model.trim();if(!model)throw new Error('请填写模型名称。');
    const url=endpoint(api.endpoint,api.provider,model),headers={'Content-Type':'application/json'},history=conversation(messages),token=key.trim();
    const loopback=['localhost','127.0.0.1','[::1]'].includes(new URL(url).hostname);
    if(!token&&!loopback)throw new Error('请在设定中填写本次会话的 API Key。');
    let body;
    if(spec.protocol==='messages'){
      if(token)headers['x-api-key']=token;
      headers['anthropic-version']='2023-06-01';
      if(typeof window!=='undefined')headers['anthropic-dangerous-direct-browser-access']='true';
      if(api.workspace?.trim())headers['anthropic-workspace-id']=api.workspace.trim();
      body={model,system,messages:history,max_tokens:1024,stream:false};
    }else if(spec.protocol==='gemini'){
      if(token)headers['x-goog-api-key']=token;
      body={systemInstruction:{parts:[{text:system}]},contents:history.map(m=>({role:m.role==='assistant'?'model':'user',parts:[{text:m.content}]}))};
    }else{
      if(token)headers.Authorization='Bearer '+token;
      body={model,messages:[{role:api.provider==='openai'?'developer':'system',content:system},...history],stream:false};
    }
    return {url,options:{method:'POST',headers,body:JSON.stringify(body)}};
  }
  function responseText(provider,json){
    const protocol=catalog[provider]?.protocol;let content='';
    if(json?.error)throw new Error('模型服务未完成回复，请检查模型名称与账号权限。');
    if(protocol==='messages')content=Array.isArray(json?.content)?json.content.filter(p=>p.type==='text').map(p=>p.text||'').join('\n'):'';
    else if(protocol==='gemini'){
      if(json?.promptFeedback?.blockReason)throw new Error('模型没有生成这条回复，请换一种表达。');
      const parts=json?.candidates?.[0]?.content?.parts;
      content=Array.isArray(parts)?parts.filter(p=>!p.thought&&typeof p.text==='string').map(p=>p.text).join('\n'):'';
    }else if(protocol==='chat'){
      content=json?.choices?.[0]?.message?.content;
      if(Array.isArray(content))content=content.filter(p=>p.type==='text').map(p=>p.text||'').join('\n');
    }
    if(typeof content!=='string'||!content.trim())throw new Error('模型没有返回文字回复。');
    return content.trim();
  }
  async function send(api,key,messages,system,{signal,timeout=45000}={}){
    const req=request(api,key,messages,system);if(!req)throw new Error('请先选择并设置模型接口。');
    const controller=new AbortController(),abort=()=>controller.abort();
    if(signal?.aborted)abort();else signal?.addEventListener('abort',abort,{once:true});
    const timer=setTimeout(abort,timeout);
    try{
      const response=await fetch(req.url,{...req.options,signal:controller.signal,credentials:'omit',redirect:'error'});
      if(!response.ok)throw new Error(`模型服务返回 ${response.status}，请检查地址、模型和 API Key。`);
      const reader=response.body.getReader();let size=0;const chunks=[];
      while(true){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>1024*1024){await reader.cancel();throw new Error('回复内容过长，请重试。');}chunks.push(value);}
      const all=new Uint8Array(size);let at=0;for(const chunk of chunks){all.set(chunk,at);at+=chunk.length;}
      return responseText(api.provider,JSON.parse(new TextDecoder().decode(all)));
    }finally{clearTimeout(timer);signal?.removeEventListener('abort',abort);}
  }
  return {catalog,cleanConfig,endpoint,request,responseText,send};
});
