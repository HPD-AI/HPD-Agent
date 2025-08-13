<!-- Updated +page.svelte -->
<script lang="ts">
    import { onMount } from 'svelte';
    import ProjectSelector from '../lib/project/ProjectSelector.svelte';
    import ConversationSidebar from '../lib/project/ConversationSidebar.svelte';
    import * as aguiClient from '@ag-ui/client';
    const { HttpAgent, EventType } = aguiClient;
    
    // Define types locally since we can't import them as named exports
    interface AGUIEvent {
        type: string;
        timestamp?: string;
        delta?: string;  // AGUI uses 'delta' for content chunks
        content?: string; // Fallback property
        message?: string; // For error events
        error?: string;   // Alternative error property
        [key: string]: any;
    }
    
    // Application state
    let currentProjectId: string | null = null;
    let currentConversationId: string | null = null;
    
    // Chat state
    let messages: Array<{role: 'user' | 'assistant', content: string, thinking?: string}> = [];
    let currentMessage = '';
    let isLoading = false;
    let streamingMessage = '';
    let currentThinking = '';
    
    // Audio recording state
    let isRecording = false;
    let mediaRecorder: MediaRecorder;
    let recordedChunks: Blob[] = [];
    let audioURL = '';
    
    const API_BASE = 'http://localhost:5135';
    
    // Load saved project/conversation from localStorage
    onMount(() => {
        if (typeof localStorage !== 'undefined') {
            currentProjectId = localStorage.getItem('currentProjectId');
            currentConversationId = localStorage.getItem('currentConversationId');
            
            // If we have both, load the conversation messages
            if (currentProjectId && currentConversationId) {
                loadConversationMessages();
            }
        }
    });
    
    // Save to localStorage when changed
    $: if (typeof localStorage !== 'undefined') {
        if (currentProjectId) {
            localStorage.setItem('currentProjectId', currentProjectId);
        } else {
            localStorage.removeItem('currentProjectId');
        }
    }
    
    $: if (typeof localStorage !== 'undefined') {
        if (currentConversationId) {
            localStorage.setItem('currentConversationId', currentConversationId);
        } else {
            localStorage.removeItem('currentConversationId');
        }
    }
    
    // Load conversation messages when conversation changes
    $: if (currentProjectId && currentConversationId) {
        loadConversationMessages();
    }
    
    async function loadConversationMessages() {
        if (!currentProjectId || !currentConversationId) return;
        
        try {
            const response = await fetch(`${API_BASE}/projects/${currentProjectId}/conversations/${currentConversationId}`);
            if (response.ok) {
                const conversation = await response.json();
                messages = conversation.messages.map((msg: any) => ({
                    role: msg.role,
                    content: msg.content,
                    thinking: undefined
                }));
            }
        } catch (error) {
            console.error('Error loading conversation messages:', error);
        }
    }
    
    function handleProjectSelected(event: CustomEvent<string>) {
        currentProjectId = event.detail;
        currentConversationId = null; // Reset conversation when project changes
        messages = []; // Clear messages
    }
    
    function handleConversationSelected(event: CustomEvent<string | null>) {
        currentConversationId = event.detail;
        if (event.detail === null) {
            messages = []; // Clear messages if no conversation selected
        }
    }
    
    function handleBackToProjects() {
        currentProjectId = null;
        currentConversationId = null;
        messages = [];
    }
    
    async function sendMessage() {
        if (!currentMessage.trim() || isLoading || !currentProjectId || !currentConversationId) return;
        
        const userMessage = currentMessage.trim();
        currentMessage = '';
        isLoading = true;
        
        // Add user message to chat
        messages = [...messages, { role: 'user', content: userMessage }];
        
        try {
            // Start streaming response with placeholder  
            streamingMessage = '';
            currentThinking = '';
            messages = [...messages, { role: 'assistant', content: '', thinking: '' }];
            
            await useStreamingEvents(userMessage);
            
        } catch (error) {
            console.error('Error sending message:', error);
            messages[messages.length - 1] = { 
                role: 'assistant', 
                content: `Error: ${error instanceof Error ? error.message : 'Unknown error'}` 
            };
            messages = [...messages];
        } finally {
            isLoading = false;
        }
    }
    
    // Streaming for real-time AGUI events with project/conversation context
    async function useStreamingEvents(userMessage: string) {
        return new Promise<void>((resolve, reject) => {
            fetch(`${API_BASE}/agent/projects/${currentProjectId}/conversations/${currentConversationId}/stream`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    threadId: currentConversationId,
                    messages: [{ content: userMessage }]
                })
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                const reader = response.body?.getReader();
                if (!reader) {
                    throw new Error('No response body reader');
                }
                
                const decoder = new TextDecoder();
                
                function processStream(): void {
                    reader!.read().then(({ done, value }) => {
                        if (done) {
                            console.log('🏁 Stream completed');
                            resolve();
                            return;
                        }
                        
                        const chunk = decoder.decode(value, { stream: true });
                        console.log('📦 Raw chunk received:', chunk);
                        
                        const lines = chunk.split('\n');
                        for (const line of lines) {
                            if (line.startsWith('event: ')) {
                                const eventType = line.substring(7);
                                console.log('🎯 Event type:', eventType);
                            } else if (line.startsWith('data: ')) {
                                try {
                                    const data = JSON.parse(line.substring(6));
                                    console.log('📡 AGUI Event Received:', JSON.stringify(data, null, 2));
                                    handleAGUIEvent(data);
                                } catch (e) {
                                    console.error('Error parsing event data:', e);
                                }
                            }
                        }
                        
                        processStream();
                    }).catch(reject);
                }
                
                processStream();
            })
            .catch(error => {
                console.error('Streaming error:', error);
                reject(error);
            });
            
            setTimeout(() => {
                console.log('⏰ Stream timeout');
                resolve();
            }, 30000);
        });
    }
    
    function handleAGUIEvent(event: AGUIEvent) {
        console.log('🎯 Processing AGUI Event:', event.type, JSON.stringify(event, null, 2));
        
        switch (event.type) {
            case 'text_message_content':
            case EventType.TEXT_MESSAGE_CONTENT:
                if (event.delta) {
                    streamingMessage += event.delta;
                    updateLastMessage();
                    console.log('📝 Added text content:', event.delta);
                } else if (event.content) {
                    streamingMessage += event.content;
                    updateLastMessage();
                    console.log('📝 Added text content (via content):', event.content);
                }
                break;
                
            case 'thinking_text_message_content':
            case EventType.THINKING_TEXT_MESSAGE_CONTENT:
                if (event.delta) {
                    currentThinking = event.delta;
                    updateLastMessage();
                    console.log('💭 Thinking:', event.delta);
                } else if (event.content) {
                    currentThinking = event.content;
                    updateLastMessage();
                    console.log('💭 Thinking (via content):', event.content);
                }
                break;
                
            case 'run_error':
            case EventType.RUN_ERROR:
                messages[messages.length - 1] = { 
                    role: 'assistant', 
                    content: `Error: ${event.error || event.message || 'Unknown error'}` 
                };
                messages = [...messages];
                isLoading = false;
                console.log('❌ Run error:', event);
                break;
                
            case 'run_finished':
            case EventType.RUN_FINISHED:
                isLoading = false;
                console.log('🏁 Run finished');
                break;
                
            case 'run_started':
                console.log('🚀 Run started');
                break;
                
            case 'text_message_start':
                console.log('📝 Text message started');
                streamingMessage = '';
                break;
                
            case 'text_message_end':
                console.log('📝 Text message ended');
                break;
                
            default:
                console.log('❓ Unhandled AGUI event type:', event.type, event);
        }
    }
    
    function updateLastMessage() {
        if (messages.length > 0) {
            messages[messages.length - 1] = { 
                role: 'assistant', 
                content: streamingMessage,
                thinking: currentThinking || undefined
            };
            messages = [...messages];
        }
    }
    
    function handleKeypress(event: KeyboardEvent) {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            sendMessage();
        }
    }
    
    // Audio recording functions
    async function startRecording() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            recordedChunks = [];
            mediaRecorder = new MediaRecorder(stream);
            mediaRecorder.ondataavailable = (e) => { if (e.data.size > 0) recordedChunks.push(e.data); };
            mediaRecorder.onstop = () => {
                const blob = new Blob(recordedChunks, { type: 'audio/webm' });
                audioURL = URL.createObjectURL(blob);
                console.log('Recorded audio blob:', blob);
                processRecordedAudio(blob);
            };
            mediaRecorder.start();
            isRecording = true;
        } catch (err) {
            console.error('Microphone access denied:', err);
        }
    }
    
    function stopRecording() {
        mediaRecorder?.stop();
        isRecording = false;
    }
    
    async function recordDirectToAgent() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            recordedChunks = [];
            
            mediaRecorder = new MediaRecorder(stream);
            mediaRecorder.ondataavailable = (event) => {
                recordedChunks.push(event.data);
            };
            mediaRecorder.onstop = () => {
                const blob = new Blob(recordedChunks, { type: 'audio/webm' });
                console.log('Recorded audio blob for direct agent:', blob);
                
                audioURL = URL.createObjectURL(blob);
                processAudioDirectToAgent(blob);
            };
            
            mediaRecorder.start();
            isRecording = true;
        } catch (err) {
            console.error('Microphone access denied:', err);
        }
    }
    
    async function processRecordedAudio(blob: Blob) {
        if (!currentProjectId || !currentConversationId) return;
        
        isLoading = true;
        try {
            const resp = await fetch(`${API_BASE}/agent/projects/${currentProjectId}/conversations/${currentConversationId}/stt`, {
                method: 'POST',
                headers: { 'Content-Type': 'audio/webm' },
                body: blob,
                credentials: 'include'
            });
            
            if (!resp.ok) {
                const errorText = await resp.text();
                console.error('STT server error:', resp.status, errorText);
                throw new Error(`Server error ${resp.status}: ${errorText}`);
            }
            
            const data = await resp.json();
            const transcript: string = data.transcript || '';
            
            currentMessage = transcript;
            await sendMessage();
        } catch (err) {
            console.error('STT error:', err);
        } finally {
            isLoading = false;
        }
    }
    
    async function processAudioDirectToAgent(blob: Blob) {
        if (!currentProjectId || !currentConversationId) return;
        
        isLoading = true;
        try {
            const resp = await fetch(`${API_BASE}/agent/projects/${currentProjectId}/conversations/${currentConversationId}/stt`, {
                method: 'POST',
                headers: { 'Content-Type': 'audio/webm' },
                body: blob,
                credentials: 'include'
            });
            
            if (!resp.ok) {
                const errorText = await resp.text();
                console.error('STT server error:', resp.status, errorText);
                throw new Error(`Server error ${resp.status}: ${errorText}`);
            }
            
            const data = await resp.json();
            const transcript: string = data.transcript || '';
            
            messages = [...messages, { role: 'user', content: transcript }];
            
            streamingMessage = '';
            currentThinking = '';
            messages = [...messages, { role: 'assistant', content: '', thinking: '' }];
            await useStreamingEvents(transcript);
        } catch (err) {
            console.error('STT error:', err);
        } finally {
            isLoading = false;
        }
    }
</script>

<!-- Main Application Layout -->
<div class="h-screen flex">
    {#if !currentProjectId}
        <!-- Project Selection Screen -->
        <div class="w-full">
            <ProjectSelector on:projectSelected={handleProjectSelected} />
        </div>
    {:else}
        <!-- Project View with Sidebar -->
        <div class="w-80 flex-shrink-0">
            <ConversationSidebar 
                projectId={currentProjectId}
                selectedConversationId={currentConversationId}
                on:conversationSelected={handleConversationSelected}
                on:backToProjects={handleBackToProjects}
            />
        </div>
        
        <!-- Chat Interface -->
        <div class="flex-1 flex flex-col">
            {#if !currentConversationId}
                <!-- No Conversation Selected -->
                <div class="flex-1 flex items-center justify-center bg-gray-50">
                    <div class="text-center">
                        <div class="text-gray-400 mb-4">
                            <svg class="mx-auto h-16 w-16" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                            </svg>
                        </div>
                        <h3 class="text-lg font-medium text-gray-700 mb-2">Select a Conversation</h3>
                        <p class="text-gray-500 mb-4">Choose a conversation from the sidebar or create a new one to start chatting</p>
                    </div>
                </div>
            {:else}
                <!-- Chat Interface -->
                <div class="flex-1 flex flex-col bg-gray-50">
                    <!-- Chat Header -->
                    <div class="bg-white border-b border-gray-200 px-6 py-4">
                        <h1 class="text-xl font-semibold text-gray-800">
                            HPD-Agent Chat
                        </h1>
                        <p class="text-sm text-gray-500">
                            Conversation in project • {messages.length} messages
                        </p>
                    </div>
                    
                    <!-- Chat Messages -->
                    <div class="flex-1 overflow-y-auto p-6 space-y-4">
                        {#each messages as message}
                            <div class="flex {message.role === 'user' ? 'justify-end' : 'justify-start'}">
                                <div class="max-w-xs lg:max-w-md px-4 py-2 rounded-lg {
                                    message.role === 'user' 
                                        ? 'bg-blue-500 text-white' 
                                        : 'bg-white text-gray-800 shadow-sm border border-gray-200'
                                }">
                                    <div class="text-sm font-medium mb-1">
                                        {message.role === 'user' ? 'You' : 'Agent'}
                                    </div>
                                    
                                    <!-- Show thinking process if available -->
                                    {#if message.thinking && message.thinking.trim()}
                                        <div class="text-xs italic text-gray-600 mb-2 border-l-2 border-gray-300 pl-2">
                                            💭 {message.thinking}
                                        </div>
                                    {/if}
                                    
                                    <div class="whitespace-pre-wrap">{message.content}</div>
                                    
                                    <!-- Show loading state for current message -->
                                    {#if message.role === 'assistant' && isLoading && message === messages[messages.length - 1]}
                                        <div class="mt-2 flex items-center text-xs text-gray-500">
                                            <div class="animate-spin rounded-full h-3 w-3 border-b-2 border-gray-500 mr-2"></div>
                                            {currentThinking || 'Processing...'}
                                        </div>
                                    {/if}
                                </div>
                            </div>
                        {/each}
                        
                        {#if isLoading && messages.length === 0}
                            <div class="flex justify-start">
                                <div class="bg-white text-gray-800 px-4 py-2 rounded-lg shadow-sm border border-gray-200">
                                    <div class="text-sm font-medium mb-1">Agent</div>
                                    <div class="animate-pulse">Initializing...</div>
                                </div>
                            </div>
                        {/if}
                    </div>
                    
                    <!-- Message Input -->
                    <div class="bg-white border-t border-gray-200 p-6">
                        <div class="flex space-x-2">
                            <input
                                bind:value={currentMessage}
                                on:keypress={handleKeypress}
                                placeholder="Type your message here..."
                                disabled={isLoading}
                                class="flex-1 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50"
                            />
                            <button
                                on:click={isRecording ? stopRecording : startRecording}
                                aria-label={isRecording ? 'Stop recording' : 'Record for chat'}
                                class="p-2 rounded-full focus:outline-none focus:ring-2 focus:ring-blue-500"
                                class:is-recording={isRecording}
                                disabled={isLoading}
                            >
                                {#if isRecording}
                                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-red-600" fill="currentColor" viewBox="0 0 24 24">
                                        <rect x="6" y="6" width="12" height="12" />
                                    </svg>
                                {:else}
                                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-blue-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 18v3m0 0h3m-3 0H9m6-3a6 6 0 01-12 0V9a6 6 0 0112 0v6z" />
                                    </svg>
                                {/if}
                            </button>
                            <button
                                on:click={recordDirectToAgent}
                                aria-label="Record direct to agent"
                                class="p-2 rounded-full focus:outline-none focus:ring-2 focus:ring-green-500 bg-green-100 hover:bg-green-200"
                                disabled={isLoading || isRecording}
                            >
                                <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                </svg>
                            </button>
                            <button
                                on:click={sendMessage}
                                disabled={isLoading || !currentMessage.trim()}
                                class="px-6 py-2 bg-blue-500 text-white rounded-md hover:bg-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {isLoading ? 'Sending...' : 'Send'}
                            </button>
                        </div>
                    </div>
                    
                    {#if audioURL}
                        <div class="px-6 pb-4">
                            <audio controls src={audioURL} class="w-full"></audio>
                        </div>
                    {/if}
                </div>
            {/if}
        </div>
    {/if}
</div>

<!-- Connection Status (only show when in chat) -->
{#if currentProjectId && currentConversationId}
    <div class="fixed bottom-4 right-4 bg-white rounded-lg shadow-md px-4 py-2 border border-gray-200">
        <div class="text-sm text-gray-600">
            Status: 
            <span class="font-medium text-green-600">
                Ready (AGUI Events Enabled)
            </span>
        </div>
        {#if isLoading}
            <div class="text-xs text-blue-600 mt-1">
                💡 {currentThinking || 'Processing...'}
            </div>
        {/if}
        <div class="text-xs text-gray-500 mt-1">
            💡 DevTools Console shows real-time events
        </div>
    </div>
{/if}