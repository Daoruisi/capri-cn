// dsh-capri-bridge — 把 DSH 会话的实时状态写到本地文件，让 Capri 桌宠能显示/播报。
//
// 只读不写：订阅会话总线，把「正在思考 / 正在跑哪个工具 / 这一轮完成了」投影成 JSON，
// 写到 %LOCALAPPDATA%\Capri\dsh-state.json。Capri 每秒轮询这个文件，不做任何 IPC。
//
// 事件形状（DSH 0.2.x，参考 dsh-tauri-pet 的 reducer）：
//   session/event(session, { type, seq, time, data })
//     turn/start | turn/end | step/start | tool/call | tool/result | assistant/message | session/title
//   agent/status({ status, agent })  status === 'idle' 兜底
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'

export const name = 'dsh-capri-bridge'

const DIR = path.join(process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'), 'Capri')
const FILE = path.join(DIR, 'dsh-state.json')

// 工具名 → 她嘴里说的动作
const VERBS = {
  pwsh: '跑命令', powershell: '跑命令', shell: '跑命令', exec: '跑命令',
  read: '看文件', read_file: '看文件',
  write: '写文件', write_file: '写文件',
  edit: '改文件', edit_file: '改文件', apply_patch: '改文件',
  glob: '找文件', grep: '搜代码',
  web_search: '上网搜', web_fetch: '读网页',
  subagent: '派子任务', subagent_fork: '派子任务',
  todo_write: '列计划', present: '交结果', ask_user_question: '问你问题',
  workflow: '跑工作流', skill: '查技能'
}

function hintOf(args) {
  if (!args || typeof args !== 'object') return ''
  const pick = args.description || args.command || args.file_path || args.path || args.query || args.prompt || args.task || args.name || ''
  return typeof pick === 'string' ? pick.replace(/\s+/g, ' ').trim().slice(0, 70) : ''
}

function detailOf(call) {
  const tool = (call && call.name) || '工具'
  const verb = VERBS[tool] || tool
  const hint = hintOf(call && call.arguments)
  return hint ? `${verb}：${hint}` : verb
}

function textOf(content) {
  if (typeof content === 'string') return content
  if (Array.isArray(content)) return content.map((part) => (typeof part === 'string' ? part : part && part.text) || '').join(' ')
  return ''
}

export function apply(ctx) {
  const state = {
    at: 0,
    sessionId: null,
    title: '',
    status: 'idle',
    detail: '',
    summary: '',
    think: '',
    turnTokens: 0,
    dayTokens: 0,
    totalTokens: 0,
    actions: 0,
    approval: false,
    doneAt: 0,
    doneId: 0
  }
  let timer = null

  const flush = () => {
    timer = null
    state.at = Date.now()
    try {
      fs.mkdirSync(DIR, { recursive: true })
      const temp = FILE + '.tmp'
      fs.writeFileSync(temp, JSON.stringify(state), 'utf8')
      fs.renameSync(temp, FILE)
    } catch {
      // 桌面宠读不到就算了，不能影响会话
    }
  }
  const soon = () => {
    if (timer) return
    timer = setTimeout(flush, 250)
  }

  const onSessionEvent = (session, event) => {
    if (!event || typeof event.type !== 'string') return
    const id = (session && session.id) || event.sessionId
    if (id) state.sessionId = id
    const data = event.data || {}

    switch (event.type) {
      case 'session/title':
        if (typeof data.title === 'string' && data.title) state.title = data.title
        break
      case 'turn/start':
        state.status = 'thinking'
        state.detail = ''
        state.summary = ''
        state.think = ''
        state.turnTokens = 0
        state.actions = 0
        state.approval = false
        break
      case 'approval/asked':
        state.approval = true
        state.status = 'waiting'
        break
      case 'approval/decided':
        state.approval = false
        break
      case 'tool/call':
        state.status = 'working'
        state.detail = detailOf(data)
        state.actions += 1
        break
      case 'tool/result':
        if (state.status === 'working') state.detail = state.detail || ''
        break
      case 'assistant/message': {
        const text = textOf(data.content ?? data.message ?? data.text).replace(/\s+/g, ' ').trim()
        if (text) { state.summary = text.slice(0, 160); state.think = text.slice(0, 120) }
        const usage = data.usage
        if (usage && typeof usage === 'object') {
          const n = (Number(usage.inputTokens) || 0) + (Number(usage.cacheReadTokens) || 0) + (Number(usage.outputTokens) || 0)
          state.turnTokens += n
          state.totalTokens += n
          state.dayTokens += n
        }
        break
      }
      case 'turn/end':
        state.status = 'done'
        state.detail = ''
        state.doneAt = Date.now()
        state.doneId += 1
        break
      default:
        return
    }
    soon()
  }

  const onStatus = (payload) => {
    if (!payload) return
    if (payload.status === 'idle' && state.status !== 'done') {
      state.status = 'idle'
      soon()
    }
  }

  ctx.on('session/event', onSessionEvent)
  ctx.on('agent/status', onStatus)
  flush()

  ctx.effect(() => () => {
    if (timer) clearTimeout(timer)
  })
}
