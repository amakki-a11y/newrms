import { useState } from 'react'

export default function useChatData() {
  return {
    messages: [], sendMessage: () => {},
    connected: false, streaming: false
  }
}
